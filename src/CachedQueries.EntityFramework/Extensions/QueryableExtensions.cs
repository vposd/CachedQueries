using System.Linq.Expressions;
using System.Reflection;
using CachedQueries.Core.Enums;
using CachedQueries.Core.Interfaces;
using CachedQueries.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace CachedQueries.EntityFramework.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    ///     Cache and return query results with write-through strategy.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        TimeSpan expire,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var key = cacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.ToListAsync(cancellationToken);

        var cacheStore = cacheManager.CacheStoreProvider.GetCacheStore(key, tags, CacheContentType.Collection);
        var cached = await cacheStore.GetAsync<IEnumerable<T>>(key, true, cancellationToken);
        if (cached is not null)
            return cached.ToList();

        var value = await query.ToListAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, true, expire, cancellationToken);
        await cacheManager.CacheInvalidator.LinkTagsAsync(key, tags, cancellationToken);

        return value;
    }

    /// <summary>
    ///     Cache and return query results with write-through strategy.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        return await query.ToCachedListAsync(tags, cacheManager.CacheOptions.DefaultExpiration, cancellationToken);
    }

    /// <summary>
    ///     Cache query results with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        TimeSpan expire,
        CancellationToken cancellationToken = default) where T : class
    {
        var tags = RetrieveRawInvalidationTagsFromQuery(query);
        return query.ToCachedListAsync(tags, expire, cancellationToken);
    }

    /// <summary>
    ///     Cache query results with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var tags = RetrieveRawInvalidationTagsFromQuery(query);
        return await query.ToCachedListAsync(tags, cacheManager.CacheOptions.DefaultExpiration, cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        TimeSpan expire,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var key = cacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cacheStore = cacheManager.CacheStoreProvider.GetCacheStore(key, tags, CacheContentType.Object);
        var cached = await cacheStore.GetAsync<T>(key, true, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await query.FirstOrDefaultAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, true, expire, cancellationToken);
        await cacheManager.CacheInvalidator.LinkTagsAsync(key, tags, cancellationToken);

        return value;
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy
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
        var cacheManager = CacheManagerContainer.Resolve();
        return await query.CachedFirstOrDefaultAsync(tags, cacheManager.CacheOptions.DefaultExpiration,
            cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        IReadOnlyCollection<string> tags,
        TimeSpan expire,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        query = query.Where(predicate);
        var key = cacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cacheStore = cacheManager.CacheStoreProvider.GetCacheStore(key, tags, CacheContentType.Object);
        var cached = await cacheStore.GetAsync<T>(key, true, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await query.FirstOrDefaultAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, true, expire, cancellationToken);
        await cacheManager.CacheInvalidator.LinkTagsAsync(key, tags, cancellationToken);

        return value;
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
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
        var tags = RetrieveRawInvalidationTagsFromQuery(query);
        return query.CachedFirstOrDefaultAsync(tags, expire, cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var tags = RetrieveRawInvalidationTagsFromQuery(query);
        return await query.CachedFirstOrDefaultAsync(tags, cacheManager.CacheOptions.DefaultExpiration,
            cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
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
        var tags = RetrieveRawInvalidationTagsFromQuery(query);
        return query.CachedFirstOrDefaultAsync(predicate, tags, expire, cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        return await query.CachedFirstOrDefaultAsync(predicate, tags, cacheManager.CacheOptions.DefaultExpiration,
            cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) where T : class
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var tags = RetrieveRawInvalidationTagsFromQuery(query);
        return await query.CachedFirstOrDefaultAsync(predicate, tags, cacheManager.CacheOptions.DefaultExpiration,
            cancellationToken);
    }

    private static List<string> RetrieveRawInvalidationTagsFromQuery(IQueryable query)
    {
        var includedTypes = query.GetIncludeTypes();
        var tags = includedTypes
            .Select(x => x.FullName)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToList();
        return tags;
    }
}