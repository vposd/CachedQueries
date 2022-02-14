using Microsoft.EntityFrameworkCore;

namespace Lore.QueryCache.EntityFramework.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Cache and return query results with write-through strategy.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Tags for further invalidation</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<IEnumerable<T>> ToCachedListAsync<T>(this IQueryable<T> query, List<string> tags,
        CancellationToken cancellationToken = default) where T : class
    {
        var key = CacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return query;

        var cached = await CacheManager.Cache.GetAsync<IEnumerable<T>>(key);
        if (cached is not null)
            return cached;

        var value = await query.ToListAsync(cancellationToken);
        await CacheManager.Cache.SetAsync(key, value);
        await CacheManager.LinkTagsAsync(key, tags);
        return value;
    }

    /// <summary>
    /// Cache query results with write-through strategy.
    /// Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static Task<IEnumerable<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken = default) where T : class
    {
        var includedTypes = query.GetIncludeTypes();
        var tags = includedTypes
            .Select(x => x.FullName)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToList();
        return ToCachedListAsync(query, tags, cancellationToken);
    }

    /// <summary>
    /// Cache and return query first result with write-through strategy
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Linking tags for further invalidation</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query, List<string> tags,
        CancellationToken cancellationToken = default) where T : class
    {
        var key = CacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cached = await CacheManager.Cache.GetAsync<T>(key);
        if (cached is not null)
            return cached;

        var value = await query.FirstOrDefaultAsync(cancellationToken);
        await CacheManager.Cache.SetAsync(key, value);
        await CacheManager.LinkTagsAsync(key, tags);
        return value;
    }

    /// <summary>
    /// Cache and return query first result with write-through strategy.
    /// Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken = default) where T : class
    {
        var includedTypes = query.GetIncludeTypes();
        var tags = includedTypes
            .Select(x => x.FullName)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToList();
        
        return CachedFirstOrDefaultAsync(query, tags, cancellationToken);
    }
}