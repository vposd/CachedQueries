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
    public static async Task ExpireEntitiesCacheAsync(this ChangeTracker changeTracker)
    {
        var entities = changeTracker.Entries()
            .Where(e =>
                e.State is EntityState.Modified or EntityState.Deleted or EntityState.Added)
            .Select(e => e.Entity.GetType().FullName)
            .Cast<string>()
            .ToHashSet();

        await CacheManager.InvalidateCacheAsync(entities);
    }
}