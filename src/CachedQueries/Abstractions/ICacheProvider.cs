namespace CachedQueries.Abstractions;

/// <summary>
/// Abstraction for cache storage operations.
/// </summary>
public interface ICacheProvider
{
    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in cache with specified options.
    /// </summary>
    Task SetAsync<T>(string key, T value, CachingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from cache by key.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cached values associated with the specified tags.
    /// </summary>
    Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached values.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}



