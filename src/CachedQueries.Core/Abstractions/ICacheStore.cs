namespace CachedQueries.Core.Abstractions;

/// <summary>
///     Represents a cache service for storing and retrieving data in an asynchronous manner.
/// </summary>
public interface ICacheStore
{
    /// <summary>
    ///     Asynchronously retrieves a cached value by its key.
    /// </summary>
    /// <typeparam name="T">The type of the value to be retrieved.</typeparam>
    /// <param name="key">The cache key used to identify the value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the cached value, or <c>null</c> if the
    ///     key does not exist in the cache.
    /// </returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously stores a value in the cache with an optional expiration time.
    /// </summary>
    /// <typeparam name="T">The type of the value to be stored.</typeparam>
    /// <param name="key">The cache key used to store the value.</param>
    /// <param name="value">The value to be cached.</param>
    /// <param name="expire">
    ///     An optional expiration timespan after which the cached value will be invalidated. If not provided,
    ///     the default cache duration will be used.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SetAsync<T>(string key, T value, TimeSpan? expire = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously removes an entry from the cache based on the provided key.
    /// </summary>
    /// <param name="key">The cache key identifying the entry to be removed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
