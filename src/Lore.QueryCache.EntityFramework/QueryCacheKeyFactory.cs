using Microsoft.EntityFrameworkCore;

namespace Lore.QueryCache.EntityFramework;

/// <summary>
/// Default cache query factory for EF workflow.
/// </summary>
public class QueryCacheKeyFactory : CacheKeyFactory
{
    /// <summary>
    /// Returns cache key as hash of query string plus joined tags
    /// </summary>
    /// <param name="query">Query param</param>
    /// <param name="tags">Linking tags for further invalidation</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>The cache key</returns>
    public override string GetCacheKey<T>(IQueryable<T> query, IEnumerable<string> tags) where T : class
    {
        var command = query.ToQueryString() + string.Join('_', tags.ToList());
        return GetStringSha256Hash(command);
    }
}