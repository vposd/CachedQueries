using System.Linq.Expressions;
using CachedQueries.Core.Enums;
using CachedQueries.Core.Interfaces;
using CachedQueries.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.EntityFramework.Extensions.Queryable;

public static class QuerySingleExtensions
{
    /// <summary>
    ///     Cache and return query first result with cache-aside strategy
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="explicitCacheStore">Explicit cache store</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>>? predicate = null,
        IReadOnlyCollection<string>? tags = null,
        TimeSpan? expire = null,
        ICacheStore? explicitCacheStore = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        
        query = predicate == null ? query : query.Where(predicate);
        tags ??= query.RetrieveRawInvalidationTagsFromQuery();
        expire ??= cacheManager.CacheOptions.DefaultExpiration;
        
        var key = cacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cacheStore = explicitCacheStore ?? cacheManager.CacheStoreProvider.GetCacheStore(key, tags, CacheContentType.Object);
        var cached = await cacheStore.GetAsync<T>(key, true, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await query.FirstOrDefaultAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, true, expire, cancellationToken);
        await cacheManager.CacheInvalidator.LinkTagsAsync(key, tags, cancellationToken);

        return value;
    }
    
    /// <summary>
    ///     Cache and return query first result with cache-aside strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken) where T : class
    {
        return await query.CachedFirstOrDefaultAsync(null, null, null, null, cancellationToken);
    }
    
    /// <summary>
    ///     Cache and return query first result with cache-aside strategy
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default) where T : class
    {
        return await query.CachedFirstOrDefaultAsync(null, tags, null, null, cancellationToken);
    }
    
    /// <summary>
    ///     Cache and return query first result with cache-aside strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <param name="expire">Expiration timespan</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        TimeSpan expire,
        CancellationToken cancellationToken = default) where T : class
    {
        return query.CachedFirstOrDefaultAsync(null, null, expire, null, cancellationToken);
    }
    
    /// <summary>
    ///     Cache and return query first result with cache-aside strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        TimeSpan expire,
        CancellationToken cancellationToken = default) where T : class
    {
        return query.CachedFirstOrDefaultAsync(predicate, null, expire, null, cancellationToken);
    }
}
