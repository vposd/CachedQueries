namespace CachedQueries.Core.Interfaces;

/// <summary>
///     Cache key factory service interface
/// </summary>
public interface ICacheKeyFactory
{
    /// <summary>
    ///     Returns cache key for the query
    /// </summary>
    /// <param name="query">Query param</param>
    /// <param name="tags">Linking tags for further invalidation</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>The cache key</returns>
    string GetCacheKey<T>(IQueryable<T> query, IEnumerable<string> tags) where T : class;
}
