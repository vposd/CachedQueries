using CachedQueries.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CachedQueries.EntityFramework.Extensions;

/// <summary>
///     Provides extension methods for the <see cref="ChangeTracker" /> class to facilitate cache invalidation
///     based on entity state changes in Entity Framework.
/// </summary>
public static class ChangeTrackerExtensions
{
    /// <summary>
    ///     Invalidates the cache for entities affected by the current change tracker state.
    ///     Utilizes implicit tags for cache management.
    /// </summary>
    /// <param name="changeTracker">The <see cref="ChangeTracker" /> instance.</param>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains
    ///     the types of entities that were affected.
    /// </returns>
    public static async Task<IEnumerable<Type>> ExpireEntitiesCacheAsync(
        this ChangeTracker changeTracker,
        CancellationToken cancellationToken)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var (types, tags) = changeTracker.GetAffectedReferences();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(tags, cancellationToken);
        return types.ToList();
    }

    /// <summary>
    ///     Retrieves the types of entities affected by the current change tracker state and the associated
    ///     invalidation tags based on their type names.
    /// </summary>
    /// <param name="changeTracker">The <see cref="ChangeTracker" /> instance.</param>
    /// <returns>
    ///     A tuple containing an array of affected entity types and an array of associated
    ///     invalidation tags.
    /// </returns>
    public static (Type[] Types, string[] Tags) GetAffectedReferences(this ChangeTracker changeTracker)
    {
        var affectedTypes = changeTracker.Entries()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted or EntityState.Added)
            .Select(x => x.Entity.GetType())
            .Distinct()
            .ToArray();

        var tags = affectedTypes
            .Select(e => e.FullName)
            .Cast<string>()
            .Distinct()
            .ToArray();

        return (affectedTypes, tags);
    }
}
