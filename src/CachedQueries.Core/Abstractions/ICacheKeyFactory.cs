namespace CachedQueries.Core.Abstractions;

/// <summary>
/// Provides an interface for a service responsible for generating cache keys for queries.
/// </summary>
public interface ICacheKeyFactory
{
    /// <summary>
    /// Generates a unique cache key for the specified query, incorporating optional tags for cache invalidation.
    /// </summary>
    /// <typeparam name="T">The type of entities being queried.</typeparam>
    /// <param name="query">The query parameter used to generate the cache key. This should be an IQueryable representing the data source.</param>
    /// <param name="tags">An array of strings representing linking tags that can be used for further cache invalidation.</param>
    /// <returns>
    /// A string representing the unique cache key for the provided query and tags. This key will be used to store and retrieve cached results.
    /// </returns>
    string GetCacheKey<T>(IQueryable<T> query, string[] tags);
}
