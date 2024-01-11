using System.Linq.Expressions;
using CachedQueries.Core.Enums;
using CachedQueries.Core.Interfaces;
using CachedQueries.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.EntityFramework.Extensions.Queryable;

public static class QueryCollectionsExtensions
{
    /// <summary>
    ///     Cache and return query results with cache-aside strategy.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="explicitCacheStore">Explicit cache store</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string>? tags = null,
        TimeSpan? expire = null,
        ICacheStore? explicitCacheStore = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        
        tags ??= query.RetrieveRawInvalidationTagsFromQuery();
        expire ??= cacheManager.CacheOptions.DefaultExpiration;
        
        var key = cacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.ToListAsync(cancellationToken);

        var cacheStore = explicitCacheStore ?? cacheManager.CacheStoreProvider.GetCacheStore(key, tags, CacheContentType.Collection);
        var cached = await cacheStore.GetAsync<IEnumerable<T>>(key, true, cancellationToken);
        if (cached is not null)
            return cached.ToList();

        var value = await query.ToListAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, true, expire, cancellationToken);
        await cacheManager.CacheInvalidator.LinkTagsAsync(key, tags, cancellationToken);

        return value;
    }
    
    /// <summary>
    ///     Cache query results with cache-aside strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken) where T : class
    {
        return await query.ToCachedListAsync(tags, null, null, cancellationToken);
    }
    
    /// <summary>
    ///     Cache query results with cache-aside strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        TimeSpan expire,
        CancellationToken cancellationToken) where T : class
    {
        return await query.ToCachedListAsync(null, expire, null, cancellationToken);
    }
    
    /// <summary>
    ///     Cache query results with cache-aside strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken) where T : class
    {
        return await query.ToCachedListAsync(null, null, null, cancellationToken);
    }
}
