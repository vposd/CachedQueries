using System.Linq.Expressions;
using CachedQueries.Core.Models;
using CachedQueries.DependencyInjection;

namespace CachedQueries.Linq;

public static class FirstOrDefaultExtensions
{
    public static async Task<T?> FirstOrDefaultCachedAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var result = await cacheManager.CacheEntryStrategy.ExecuteAsync(query.Where(predicate), options, cancellationToken);
        return result;
    }
    
    public static async Task<T?> FirstOrDefaultCachedAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var defaultOptions = new CachingOptions
        {
            CacheDuration = cacheManager.Config.DefaultCacheDuration
        };
       
        return await query.FirstOrDefaultCachedAsync(predicate, defaultOptions, cancellationToken);
    }
    
    public static async Task<T?> FirstOrDefaultCachedAsync<T>(this IQueryable<T> query,
        CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var result = await cacheManager.CacheEntryStrategy.ExecuteAsync(query, options, cancellationToken);
        return result;
    }
    
    public static async Task<T?> FirstOrDefaultCachedAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var defaultOptions = new CachingOptions
        {
            CacheDuration = cacheManager.Config.DefaultCacheDuration
        };

        return await query.FirstOrDefaultCachedAsync(defaultOptions, cancellationToken);
    }
}
