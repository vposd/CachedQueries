using System.Data.Common;
using CachedQueries.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Interceptors;

/// <summary>
///     EF Core interceptor that runs deferred cache invalidation when an EF transaction commits,
///     and discards it on rollback. Works together with <see cref="CacheInvalidationInterceptor" />.
/// </summary>
/// <remarks>
///     Only transactions committed through EF are observed. When a context joins an external transaction via
///     <c>Database.UseTransaction(dbTransaction)</c> and that transaction is committed outside EF (or through another
///     context), no commit event is raised for this context — invalidate manually after committing.
/// </remarks>
public sealed class TransactionCacheInvalidationInterceptor(
    ICacheInvalidator invalidator,
    ILogger<TransactionCacheInvalidationInterceptor>? logger = null)
    : DbTransactionInterceptor
{
    /// <inheritdoc />
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (eventData.Context is not null)
        {
            var entityTypes = PendingInvalidations.Take(eventData.Context);
            if (entityTypes is not null)
            {
                InvalidationRunner.Invalidate(invalidator, entityTypes, logger, "after transaction commit");
            }
        }

        base.TransactionCommitted(transaction, eventData);
    }

    /// <inheritdoc />
    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var entityTypes = PendingInvalidations.Take(eventData.Context);
            if (entityTypes is not null)
            {
                await InvalidationRunner.InvalidateAsync(invalidator, entityTypes, logger,
                    "after transaction commit", cancellationToken).ConfigureAwait(false);
            }
        }

        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        DiscardPending(eventData);
        base.TransactionRolledBack(transaction, eventData);
    }

    /// <inheritdoc />
    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardPending(eventData);
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }

    // TransactionFailed is intentionally not handled: a failed savepoint or rollback does not end the
    // transaction, and a failed commit wrote nothing. Keeping pending invalidations can at worst cause an
    // extra invalidation later; the state is released together with the DbContext.

    private void DiscardPending(TransactionEndEventData eventData)
    {
        if (eventData.Context is not null)
        {
            PendingInvalidations.Discard(eventData.Context);
            logger?.LogDebug("Transaction rolled back, discarding pending cache invalidations");
        }
    }
}
