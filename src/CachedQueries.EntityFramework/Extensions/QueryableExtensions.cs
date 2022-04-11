using System.Linq.Expressions;
using CachedQueries.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CachedQueries.EntityFramework.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Cache and return query results with write-through strategy.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        TimeSpan? expire = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var key = CacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.ToListAsync(cancellationToken);

        var cached = await CacheManager.Cache.GetAsync<IEnumerable<T>>(key, cancellationToken);
        if (cached is not null)
            return cached.ToList();

        var value = await query.ToListAsync(cancellationToken);
        return await SetCacheDataAsync(key, value, tags, expire, cancellationToken);
    }

    /// <summary>
    /// Cache query results with write-through strategy.
    /// Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        TimeSpan? expire = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var tags = RetrieveInvalidationTagsFromQuery(query);
        return query.ToCachedListAsync(tags, expire, cancellationToken);
    }
    
    /// <summary>
    /// Cache query results with write-through strategy.
    /// Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken) where T : class
    {
        var tags = RetrieveInvalidationTagsFromQuery(query);
        return query.ToCachedListAsync(tags, null, cancellationToken);
    }

    /// <summary>
    /// Cache and return query first result with write-through strategy
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOfDefaultAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        TimeSpan? expire = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var key = CacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cached = await CacheManager.Cache.GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await query.FirstOrDefaultAsync(cancellationToken);
        return await SetCacheDataAsync(key, value, tags, expire, cancellationToken);
    }

    /// <summary>
    /// Cache and return query first result with write-through strategy
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOfDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        IReadOnlyCollection<string> tags,
        TimeSpan? expire = null,
        CancellationToken cancellationToken = default) where T : class
    {
        query = query.Where(predicate);
        var key = CacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cached = await CacheManager.Cache.GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await query.FirstOrDefaultAsync(cancellationToken);
        return await SetCacheDataAsync(key, value, tags, expire, cancellationToken);
    }

    /// <summary>
    /// Cache and return query first result with write-through strategy.
    /// Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <param name="expire">Expiration timespan</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static Task<T?> CachedFirstOfDefaultAsync<T>(this IQueryable<T> query,
        TimeSpan? expire = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var tags = RetrieveInvalidationTagsFromQuery(query);
        return query.CachedFirstOfDefaultAsync(tags, expire, cancellationToken);
    }
    
    /// <summary>
    /// Cache and return query first result with write-through strategy.
    /// Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static Task<T?> CachedFirstOfDefaultAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken) where T : class
    {
        var tags = RetrieveInvalidationTagsFromQuery(query);
        return query.CachedFirstOfDefaultAsync(tags, null, cancellationToken);
    }

    /// <summary>
    /// Cache and return query first result with write-through strategy.
    /// Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static Task<T?> CachedFirstOfDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        TimeSpan? expire,
        CancellationToken cancellationToken = default) where T : class
    {
        var tags = RetrieveInvalidationTagsFromQuery(query);
        return query.CachedFirstOfDefaultAsync(predicate, tags, expire, cancellationToken);
    }
    
    /// <summary>
    /// Cache and return query first result with write-through strategy.
    /// Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static Task<T?> CachedFirstOfDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) where T : class
    {
        var tags = RetrieveInvalidationTagsFromQuery(query);
        return query.CachedFirstOfDefaultAsync(predicate, tags, null, cancellationToken);
    }
    
    private static List<string> RetrieveInvalidationTagsFromQuery<T>(IQueryable<T> query) where T : class
    {
        var includedTypes = query.GetIncludeTypes();
        var tags = includedTypes
            .Select(x => x.FullName)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToList();
        return tags;
    }

    private static async Task<T> SetCacheDataAsync<T>(string key, T value, IEnumerable<string> tags, TimeSpan? expire, CancellationToken cancellationToken)
    {
        try
        {
            await CacheManager.Cache.SetAsync(key, value, expire, cancellationToken);
            await CacheManager.LinkTagsAsync(key, tags, cancellationToken);
        }
        catch (Exception exception)
        {
            await CacheManager.Cache.DeleteAsync(key, cancellationToken);
            CacheManager.Cache.Log(LogLevel.Error, "Error setting data to cache: @{Message}", exception.Message);
            return value;
        }

        return value;
    }
}