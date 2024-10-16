using CachedQueries.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CachedQueries.EntityFramework.Extensions;

public static class ChangeTrackerExtensions
{
    /// <summary>
    ///     Invalidate cache for implicit tags approach.
    /// </summary>
    /// <param name="changeTracker"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Affected entities types</returns>
    public static async Task<IEnumerable<Type>> ExpireEntitiesCacheAsync(this ChangeTracker changeTracker,
        CancellationToken cancellationToken)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var (types, tags) = changeTracker.GetAffectedReferences();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(tags, cancellationToken);
        return types.ToList();
    }

    /// <summary>
    ///     Returns affected types and invalidation tags
    /// </summary>
    /// <param name="changeTracker"></param>
    /// <returns>Affected types and invalidation tags</returns>
    public static (Type[] Types, string[] Tags) GetAffectedReferences(
        this ChangeTracker changeTracker)
    {
        var affectedTypes = changeTracker.Entries()
            .Where(e =>
                e.State is EntityState.Modified or EntityState.Deleted or EntityState.Added)
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
