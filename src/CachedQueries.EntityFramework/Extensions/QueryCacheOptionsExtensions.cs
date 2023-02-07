using CachedQueries.DependencyInjection;

namespace CachedQueries.EntityFramework.Extensions;

public static class QueryCacheOptionsExtensions
{
    /// <summary>
    ///     Use Entity Framework workflow
    /// </summary>
    /// <returns></returns>
    public static QueryCacheOptions UseEntityFramework(this QueryCacheOptions options)
    {
        options.UseKeyFactory<QueryCacheKeyFactory>();
        return options;
    }
}
