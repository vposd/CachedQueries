using CachedQueries.Core;
using CachedQueries.Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.EntityFramework;

/// <summary>
///     Default cache query factory for EF workflow.
/// </summary>
public class QueryCacheKeyFactory(ICacheContextProvider cacheContext) : DefaultCacheKeyFactory(cacheContext)
{
    /// <summary>
    ///     Returns cache key as hash of query string plus joined tags
    /// </summary>
    /// <param name="query">Query param</param>
    /// <param name="tags">Invalidation tags</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>The cache key</returns>
    public override string GetCacheKey<T>(IQueryable<T> query, string[] tags)
    {
        var sqlString = query.ToQueryString();
        var expressionString = query.Expression.ToString();

        var tagList = tags.Select(tag => string.Join(cacheContext.GetContextKey(), tag));
        var command = sqlString + expressionString + string.Join('_', tagList.Distinct().ToList());
        return GetStringSha256Hash(command);
    }
}
