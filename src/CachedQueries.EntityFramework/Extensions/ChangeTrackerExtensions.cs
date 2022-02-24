using CachedQueries.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CachedQueries.EntityFramework.Extensions;

public static class ChangeTrackerExtensions
{
    /// <summary>
    /// Invalidate cache for implicit tags approach.
    /// </summary>
    /// <param name="changeTracker"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Affected entities types</returns>
    public static async Task<ICollection<Type>> ExpireEntitiesCacheAsync(this ChangeTracker changeTracker, CancellationToken cancellationToken = default)
    {
        var affectedTypes = changeTracker.Entries()
            .Where(e =>
                e.State is EntityState.Modified or EntityState.Deleted or EntityState.Added)
            .Select(x => x.Entity.GetType())
            .ToHashSet();
        
        var tags = affectedTypes
            .Select(e => e.FullName)
            .Cast<string>()
            .ToHashSet();

        await CacheManager.InvalidateCacheAsync(tags, cancellationToken);
        return affectedTypes;
    }
}