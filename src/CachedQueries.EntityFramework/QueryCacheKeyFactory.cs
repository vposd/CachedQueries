using CachedQueries.Core;
using CachedQueries.Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.EntityFramework;

/// <summary>
///     Default implementation of a cache key factory for Entity Framework queries.
///     This class is responsible for generating cache keys based on the query and associated tags.
/// </summary>
public class QueryCacheKeyFactory(ICacheContextProvider cacheContext) : DefaultCacheKeyFactory(cacheContext)
{
    /// <summary>
    ///     Generates a unique cache key by combining the SQL query string,
    ///     the expression string, and the provided invalidation tags.
    /// </summary>
    /// <param name="query">The IQueryable query for which to generate a cache key.</param>
    /// <param name="tags">An array of tags used for cache invalidation.</param>
    /// <typeparam name="T">The type of the entities being queried.</typeparam>
    /// <returns>A SHA256 hash string representing the cache key.</returns>
    public override string GetCacheKey<T>(IQueryable<T> query, string[] tags)
    {
        // Convert the IQueryable to its SQL string representation
        var sqlString = typeof(T).IsClass
            ? (query as IQueryable<object>)!.AsSingleQuery().ToQueryString()
            : query.ToQueryString();

        // Get the string representation of the query expression
        var expressionString = query.Expression.ToString();

        // Join tags with the context key to form a unique identifier
        var tagList = tags.Select(tag => string.Concat(cacheContext.GetContextKey(), tag));

        // Combine SQL string, expression string, and tags into a command
        var command = sqlString + expressionString + string.Join('_', tagList.Distinct().ToList());

        // Return the SHA256 hash of the combined command as the cache key
        return GetStringSha256Hash(command);
    }
}
