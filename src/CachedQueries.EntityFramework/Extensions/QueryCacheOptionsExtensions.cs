using CachedQueries.DependencyInjection;
using CachedQueries.EntityFramework.Strategies;

namespace CachedQueries.EntityFramework.Extensions;

/// <summary>
/// Provides extension methods for configuring caching options specifically for 
/// Entity Framework within the Cached Queries framework.
/// </summary>
public static class QueryCacheOptionsExtensions
{
    /// <summary>
    /// Configures the caching options to use Entity Framework-specific implementations 
    /// for cache key generation, cache collection strategy, and cache entry strategy.
    /// </summary>
    /// <param name="options">The <see cref="CachedQueriesOptions"/> instance to configure.</param>
    /// <returns>The configured <see cref="CachedQueriesOptions"/> instance for further customization.</returns>
    public static CachedQueriesOptions UseEntityFramework(this CachedQueriesOptions options)
    {
        options.UseCacheKeyFactory<QueryCacheKeyFactory>();
        options.UseCacheCollectionStrategy<CacheCollectionStrategy>();
        options.UseCacheEntryStrategy<CacheEntryStrategy>();
        return options;
    }
}
