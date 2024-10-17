namespace CachedQueries.Core.Abstractions;

/// <summary>
/// Defines the contract for a service responsible for invalidating cache entries based on specified tags.
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>
    /// Invalidates cached entries associated with the specified tags asynchronously.
    /// </summary>
    /// <param name="tags">An array of tags that identify which cached entries to invalidate.</param>
    /// <param name="cancellationToken">A cancellation token to signal the operation's cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidateCacheAsync(string[] tags, CancellationToken cancellationToken);

    /// <summary>
    /// Links a cache key with specified invalidation tags to allow for future cache invalidation.
    /// </summary>
    /// <param name="key">The cache key to be linked with tags.</param>
    /// <param name="tags">An array of tags that will be associated with the specified cache key for invalidation purposes.</param>
    /// <param name="cancellationToken">A cancellation token to signal the operation's cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LinkTagsAsync(string key, string[] tags, CancellationToken cancellationToken);
}
