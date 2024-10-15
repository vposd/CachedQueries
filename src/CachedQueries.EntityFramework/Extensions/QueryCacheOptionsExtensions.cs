using CachedQueries.DependencyInjection;
using CachedQueries.EntityFramework.Strategies;

namespace CachedQueries.EntityFramework.Extensions;

public static class QueryCacheOptionsExtensions
{
    /// <summary>
    ///     Use Entity Framework workflow
    /// </summary>
    /// <returns></returns>
    public static CachedQueriesOptions UseEntityFramework(this CachedQueriesOptions options)
    {
        options.UseCacheKeyFactory<QueryCacheKeyFactory>();
        options.UseCacheCollectionStrategy<CacheCollectionStrategy>();
        options.UseCacheEntryStrategy<CacheEntryStrategy>();
        return options;
    }
}
