using CachedQueries.Core.Models;
using CachedQueries.DependencyInjection;

namespace CachedQueries.Linq;

public static class ToListExtensions
{
    public static async Task<ICollection<T>> ToListCachedAsync<T>(this IQueryable<T> query,
        CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var result = await cacheManager.CacheCollectionStrategy.ExecuteAsync(query, options, cancellationToken);
        return result;
    }

    public static async Task<ICollection<T>> ToListCachedAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var defaultOption = new CachingOptions();
        var result = await cacheManager.CacheCollectionStrategy.ExecuteAsync(query, defaultOption, cancellationToken);
        return result;
    }
}
