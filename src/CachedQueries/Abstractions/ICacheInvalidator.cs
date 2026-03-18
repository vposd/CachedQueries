namespace CachedQueries.Abstractions;

/// <summary>
/// Handles cache invalidation when entities change.
/// Tracks cache entries and their associated entity types, tags, and context for invalidation.
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>
    /// Invalidates cache entries for the specified entity types.
    /// Only entries matching the current context (or global entries without context) are invalidated.
    /// </summary>
    /// <param name="entityTypes">The entity types whose cache entries should be invalidated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cache entries for the specified tags.
    /// Only entries matching the current context (or global entries without context) are invalidated.
    /// </summary>
    /// <param name="tags">The tags whose cache entries should be invalidated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a cache entry with its associated entity types for later invalidation.
    /// </summary>
    /// <param name="cacheKey">The cache key to register.</param>
    /// <param name="entityTypes">Entity types this cache entry depends on.</param>
    /// <param name="contextKey">
    /// The cache context key (e.g., tenant ID). Null for global (context-independent) entries.
    /// When invalidating, global entries and entries matching the current context are removed.
    /// </param>
    void RegisterCacheEntry(string cacheKey, IEnumerable<Type> entityTypes, string? contextKey = null);

    /// <summary>
    /// Registers a cache entry with custom tags for later invalidation.
    /// </summary>
    /// <param name="cacheKey">The cache key to register.</param>
    /// <param name="tags">Tags for grouped invalidation.</param>
    /// <param name="contextKey">
    /// The cache context key (e.g., tenant ID). Null for global (context-independent) entries.
    /// </param>
    void RegisterCacheEntry(string cacheKey, IEnumerable<string> tags, string? contextKey = null);

    /// <summary>
    /// Clears all cached entries across all providers.
    /// Use with caution in production environments.
    /// </summary>
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached entries for the current cache context (e.g., tenant).
    /// Only affects entries registered with the current context key.
    /// </summary>
    Task ClearContextAsync(CancellationToken cancellationToken = default);
}
