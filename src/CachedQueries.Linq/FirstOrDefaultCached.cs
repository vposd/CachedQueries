using CachedQueries.Core.Models;
using CachedQueries.DependencyInjection;

namespace CachedQueries.Linq;

public static class FirstOrDefaultExtensions
{
    public static async Task<T?> FirstOrDefaultCachedAsync<T>(this IQueryable<T> query,
        CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var result = await cacheManager.CacheEntryStrategy.ExecuteAsync(query, options, cancellationToken);
        return result;
    }
}
