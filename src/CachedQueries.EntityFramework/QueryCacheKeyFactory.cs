using CachedQueries.Core;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.EntityFramework;

/// <summary>
///     Default cache query factory for EF workflow.
/// </summary>
public class QueryCacheKeyFactory : CacheKeyFactory
{
    /// <summary>
    ///     Returns cache key as hash of query string plus joined tags
    /// </summary>
    /// <param name="query">Query param</param>
    /// <param name="tags">Invalidation tags</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>The cache key</returns>
    public override string GetCacheKey<T>(IQueryable<T> query, IEnumerable<string> tags) where T : class
    {
        var sqlString = query.ToQueryString();
        var expressionString = query.Expression.ToString();

        var command = sqlString + expressionString + string.Join('_', tags.ToList());
        return GetStringSha256Hash(command);
    }
}
