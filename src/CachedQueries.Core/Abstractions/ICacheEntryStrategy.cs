using CachedQueries.Core.Models;

namespace CachedQueries.Core.Abstractions;

/// <summary>
/// Defines the contract for a strategy that manages the execution of cache entries, including retrieval and storage logic.
/// </summary>
public interface ICacheEntryStrategy
{
    /// <summary>
    /// Executes the given query and manages the caching logic based on the specified caching options.
    /// </summary>
    /// <typeparam name="T">The type of entities being queried.</typeparam>
    /// <param name="query">The query parameter used to fetch data from the underlying data source.</param>
    /// <param name="options">The caching options that determine how the caching should be handled, including expiration settings and policies.</param>
    /// <param name="cancellationToken">A cancellation token to signal the operation's cancellation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the cached result of type T, or null if the result is not found.
    /// </returns>
    Task<T?> ExecuteAsync<T>(IQueryable<T> query, CachingOptions options,
        CancellationToken cancellationToken = default);
}
