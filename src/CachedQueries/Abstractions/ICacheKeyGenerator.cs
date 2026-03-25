using System.Linq.Expressions;

namespace CachedQueries.Abstractions;

/// <summary>
///     Generates unique cache keys for queries.
/// </summary>
public interface ICacheKeyGenerator
{
    /// <summary>
    ///     Generates a cache key for the given query.
    /// </summary>
    string GenerateKey<T>(IQueryable<T> query);

    /// <summary>
    ///     Generates a cache key for a query with additional predicate.
    /// </summary>
    string GenerateKey<T>(IQueryable<T> query, Expression<Func<T, bool>>? predicate);
}
