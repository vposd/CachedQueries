using System.Transactions;
using CachedQueries.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Interceptors;

/// <summary>
///     EF Core interceptor that automatically invalidates cache when entities are modified.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Changed entity types are taken from EF metadata, so lazy-loading proxies, inheritance (base types are
///             invalidated too), owned types (owners are invalidated) and many-to-many join entities (both sides are
///             invalidated) are handled.
///         </item>
///         <item>
///             Inside an EF transaction (<c>BeginTransaction</c>), invalidation is deferred to
///             <see cref="TransactionCacheInvalidationInterceptor" /> and runs on commit.
///         </item>
///         <item>
///             Inside an ambient <see cref="TransactionScope" />, invalidation runs when the scope commits.
///         </item>
///         <item>
///             Only <c>SaveChanges</c> is observed: <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>, raw SQL and changes made
///             by other applications are not detected — invalidate manually in those cases.
///         </item>
///     </list>
///     Invalidation failures are logged and swallowed: they never fail <c>SaveChanges</c>.
/// </remarks>
public sealed class CacheInvalidationInterceptor(
    ICacheInvalidator invalidator,
    ILogger<CacheInvalidationInterceptor>? logger = null)
    : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureChangedEntityTypes(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureChangedEntityTypes(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null && !TryDefer(eventData.Context))
        {
            var entityTypes = PendingInvalidations.Take(eventData.Context);
            if (entityTypes is not null)
            {
                InvalidationRunner.Invalidate(invalidator, entityTypes, logger, "after SaveChanges");
            }
        }

        return base.SavedChanges(eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && !TryDefer(eventData.Context))
        {
            var entityTypes = PendingInvalidations.Take(eventData.Context);
            if (entityTypes is not null)
            {
                await InvalidationRunner.InvalidateAsync(invalidator, entityTypes, logger, "after SaveChanges",
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        DiscardFailedChanges(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardFailedChanges(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static void CaptureChangedEntityTypes(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var changedTypes = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Metadata)
            .Distinct()
            .SelectMany(PendingInvalidations.GetAffectedTypes)
            .ToHashSet();

        if (changedTypes.Count > 0)
        {
            PendingInvalidations.Add(context, changedTypes);
        }
    }

    private static void DiscardFailedChanges(DbContext? context)
    {
        // Outside a transaction a failed SaveChanges wrote nothing. Inside one, earlier successful
        // SaveChanges calls may still be committed, so their pending invalidations are kept
        // (invalidating a type whose change failed is harmless).
        if (context is not null && context.Database.CurrentTransaction is null)
        {
            PendingInvalidations.Discard(context);
        }
    }

    /// <summary>
    ///     Returns true when invalidation must wait for a transaction to commit.
    /// </summary>
    private bool TryDefer(DbContext context)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            logger?.LogDebug("SaveChanges completed inside a transaction, deferring cache invalidation until commit");
            return true;
        }

        var ambient = Transaction.Current;
        if (ambient is null)
        {
            return false;
        }

        var entityTypes = PendingInvalidations.Take(context);
        if (entityTypes is not null)
        {
            logger?.LogDebug("SaveChanges completed inside a TransactionScope, deferring cache invalidation");
            ambient.TransactionCompleted += (_, e) =>
            {
                if (e.Transaction?.TransactionInformation.Status == TransactionStatus.Committed)
                {
                    InvalidationRunner.Invalidate(invalidator, entityTypes, logger, "after TransactionScope commit");
                }
            };
        }

        return true;
    }
}

/// <summary>
///     Runs invalidation for interceptors: failures are logged, never thrown.
/// </summary>
internal static class InvalidationRunner
{
    public static void Invalidate(ICacheInvalidator invalidator, IReadOnlyCollection<Type> entityTypes,
        ILogger? logger, string stage)
    {
        try
        {
            // Interceptor callbacks of synchronous SaveChanges/Commit are synchronous; library code uses
            // ConfigureAwait(false) throughout, so blocking here cannot deadlock on a SynchronizationContext.
            invalidator.InvalidateAsync(entityTypes).GetAwaiter().GetResult();
            logger?.LogDebug("Invalidated cache for {Count} entity types {Stage}", entityTypes.Count, stage);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to invalidate cache {Stage}", stage);
        }
    }

    public static async Task InvalidateAsync(ICacheInvalidator invalidator, IReadOnlyCollection<Type> entityTypes,
        ILogger? logger, string stage, CancellationToken cancellationToken)
    {
        try
        {
            await invalidator.InvalidateAsync(entityTypes, cancellationToken).ConfigureAwait(false);
            logger?.LogDebug("Invalidated cache for {Count} entity types {Stage}", entityTypes.Count, stage);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to invalidate cache {Stage}", stage);
        }
    }
}
