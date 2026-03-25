using System.Collections.Concurrent;
using CachedQueries.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Interceptors;

/// <summary>
///     EF Core interceptor that automatically invalidates cache when entities are modified.
///     When inside a transaction, invalidation is deferred to <see cref="TransactionCacheInvalidationInterceptor" />.
/// </summary>
public sealed class CacheInvalidationInterceptor(
    ICacheInvalidator invalidator,
    ILogger<CacheInvalidationInterceptor>? logger = null)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureChangedEntityTypes(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureChangedEntityTypes(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
        {
            ScheduleInvalidation(eventData.Context);
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await ScheduleInvalidationAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
        {
            ClearPendingInvalidations(eventData.Context);
        }

        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ClearPendingInvalidations(eventData.Context);
        }

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
            .Select(e => e.Entity.GetType())
            .Distinct()
            .ToList();

        if (changedTypes.Count == 0)
        {
            return;
        }

        var contextId = TransactionCacheInvalidationInterceptor.GetContextIdentifier(context);

        // Thread-safe: ConcurrentDictionary<Type, byte> acts as a concurrent HashSet
        var pending = TransactionCacheInvalidationInterceptor.PendingInvalidations
            .GetOrAdd(contextId, _ => new ConcurrentDictionary<Type, byte>());

        foreach (var type in changedTypes)
        {
            pending[type] = 0;
        }
    }

    private void ScheduleInvalidation(DbContext context)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            logger?.LogDebug("SaveChanges completed inside transaction, deferring cache invalidation until commit");
            return;
        }

        InvalidatePendingChanges(context);
    }

    private async Task ScheduleInvalidationAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            logger?.LogDebug(
                "SaveChangesAsync completed inside transaction, deferring cache invalidation until commit");
            return;
        }

        await InvalidatePendingChangesAsync(context, cancellationToken);
    }

    private static void ClearPendingInvalidations(DbContext context)
    {
        var contextId = TransactionCacheInvalidationInterceptor.GetContextIdentifier(context);
        TransactionCacheInvalidationInterceptor.PendingInvalidations.TryRemove(contextId, out _);
    }

    private void InvalidatePendingChanges(DbContext context)
    {
        var contextId = TransactionCacheInvalidationInterceptor.GetContextIdentifier(context);

        if (!TransactionCacheInvalidationInterceptor.PendingInvalidations.TryRemove(contextId, out var entityTypes) ||
            entityTypes.IsEmpty)
        {
            return;
        }

        try
        {
            invalidator.InvalidateAsync(entityTypes.Keys).GetAwaiter().GetResult();
            logger?.LogDebug("Invalidated cache for {Count} entity types", entityTypes.Count);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to invalidate cache");
        }
    }

    private async Task InvalidatePendingChangesAsync(DbContext context, CancellationToken cancellationToken)
    {
        var contextId = TransactionCacheInvalidationInterceptor.GetContextIdentifier(context);

        if (!TransactionCacheInvalidationInterceptor.PendingInvalidations.TryRemove(contextId, out var entityTypes) ||
            entityTypes.IsEmpty)
        {
            return;
        }

        try
        {
            await invalidator.InvalidateAsync(entityTypes.Keys, cancellationToken);
            logger?.LogDebug("Invalidated cache for {Count} entity types", entityTypes.Count);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to invalidate cache");
        }
    }
}
