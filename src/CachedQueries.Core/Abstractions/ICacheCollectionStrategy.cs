using CachedQueries.Core.Models;

namespace CachedQueries.Core.Abstractions;

/// <summary>
/// Defines the contract for a strategy that manages the execution of cache collections, including retrieval and storage logic for multiple items.
/// </summary>
public interface ICacheCollectionStrategy
{
    /// <summary>
    /// Executes the given query and manages the caching logic for a collection of items based on the specified caching options.
    /// </summary>
    /// <typeparam name="T">The type of entities being queried.</typeparam>
    /// <param name="query">The query parameter used to fetch data from the underlying data source.</param>
    /// <param name="options">The caching options that determine how the caching should be handled, including expiration settings and policies.</param>
    /// <param name="cancellationToken">A cancellation token to signal the operation's cancellation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a collection of cached results of type <typeparamref name="T"/>. 
    /// If no results are found, an empty collection will be returned.
    /// </returns>
    Task<ICollection<T>> ExecuteAsync<T>(IQueryable<T> query, CachingOptions options,
        CancellationToken cancellationToken = default);
}
