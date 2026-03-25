using System.Collections.Concurrent;
using System.Data.Common;
using CachedQueries.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Interceptors;

/// <summary>
///     EF Core interceptor that handles cache invalidation on transaction commit.
///     Works together with <see cref="CacheInvalidationInterceptor" />.
/// </summary>
public sealed class TransactionCacheInvalidationInterceptor(
    ICacheInvalidator invalidator,
    ILogger<TransactionCacheInvalidationInterceptor>? logger = null)
    : DbTransactionInterceptor
{
    // Track pending invalidations per DbContext instance (shared with SaveChanges interceptor)
    // Key: DbContext.ContextId.ToString() (unique per context instance + lease)
    // Value: thread-safe set of changed entity types
    internal static readonly ConcurrentDictionary<string, ConcurrentDictionary<Type, byte>>
        PendingInvalidations = new();

    internal static string GetContextIdentifier(DbContext context)
    {
        return context.ContextId.ToString();
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (eventData.Context is not null)
        {
            InvalidatePendingChanges(eventData.Context);
        }

        base.TransactionCommitted(transaction, eventData);
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await InvalidatePendingChangesAsync(eventData.Context, cancellationToken);
        }

        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (eventData.Context is not null)
        {
            var contextId = GetContextIdentifier(eventData.Context);
            PendingInvalidations.TryRemove(contextId, out _);
            logger?.LogDebug("Transaction rolled back, discarding pending cache invalidations");
        }

        base.TransactionRolledBack(transaction, eventData);
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var contextId = GetContextIdentifier(eventData.Context);
            PendingInvalidations.TryRemove(contextId, out _);
            logger?.LogDebug("Transaction rolled back, discarding pending cache invalidations");
        }

        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
    {
        if (eventData.Context is not null)
        {
            var contextId = GetContextIdentifier(eventData.Context);
            PendingInvalidations.TryRemove(contextId, out _);
        }

        base.TransactionFailed(transaction, eventData);
    }

    public override Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var contextId = GetContextIdentifier(eventData.Context);
            PendingInvalidations.TryRemove(contextId, out _);
        }

        return base.TransactionFailedAsync(transaction, eventData, cancellationToken);
    }

    private void InvalidatePendingChanges(DbContext context)
    {
        var contextId = GetContextIdentifier(context);

        if (!PendingInvalidations.TryRemove(contextId, out var entityTypes) || entityTypes.IsEmpty)
        {
            return;
        }

        try
        {
            invalidator.InvalidateAsync(entityTypes.Keys).GetAwaiter().GetResult();
            logger?.LogDebug("Invalidated cache for {Count} entity types after transaction commit", entityTypes.Count);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to invalidate cache after transaction commit");
        }
    }

    private async Task InvalidatePendingChangesAsync(DbContext context, CancellationToken cancellationToken)
    {
        var contextId = GetContextIdentifier(context);

        if (!PendingInvalidations.TryRemove(contextId, out var entityTypes) || entityTypes.IsEmpty)
        {
            return;
        }

        try
        {
            await invalidator.InvalidateAsync(entityTypes.Keys, cancellationToken);
            logger?.LogDebug("Invalidated cache for {Count} entity types after transaction commit", entityTypes.Count);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to invalidate cache after transaction commit");
        }
    }
}
