using CachedQueries.DependencyInjection;

namespace CachedQueries.Redis.Extensions;

public static class QueryCacheOptionsExtensions
{
    /// <summary>
    ///     Use Redis workflow
    /// </summary>
    /// <returns></returns>
    public static QueryCacheOptions UseRedis(this QueryCacheOptions options)
    {
        options.UseLockManager<RedisLockManager>();
        return options;
    }
}
