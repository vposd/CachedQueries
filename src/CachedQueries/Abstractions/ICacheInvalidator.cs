namespace CachedQueries.Abstractions;

/// <summary>
///     Handles cache invalidation when entities change.
///     Tracks cache entries and their associated entity types, tags, and context for invalidation.
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>
    ///     Invalidates cache entries for the specified entity types.
    ///     Only entries matching the current context (or global entries without context) are invalidated.
    /// </summary>
    /// <param name="entityTypes">The entity types whose cache entries should be invalidated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates cache entries for the specified entity types using an explicit context key.
    ///     Use this overload when there is no ambient context (e.g., in background services or event handlers).
    /// </summary>
    /// <param name="entityTypes">The entity types whose cache entries should be invalidated.</param>
    /// <param name="contextKey">The context key (e.g., tenant ID) to scope invalidation to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateAsync(IEnumerable<Type> entityTypes, string contextKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates cache entries for the specified tags.
    ///     Only entries matching the current context (or global entries without context) are invalidated.
    /// </summary>
    /// <param name="tags">The tags whose cache entries should be invalidated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates cache entries for the specified tags using an explicit context key.
    ///     Use this overload when there is no ambient context (e.g., in background services or event handlers).
    /// </summary>
    /// <param name="tags">The tags whose cache entries should be invalidated.</param>
    /// <param name="contextKey">The context key (e.g., tenant ID) to scope invalidation to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateByTagsAsync(IEnumerable<string> tags, string contextKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Registers a cache entry with its associated entity types for later invalidation.
    /// </summary>
    /// <param name="cacheKey">The cache key to register.</param>
    /// <param name="entityTypes">Entity types this cache entry depends on.</param>
    /// <param name="contextKey">
    ///     The cache context key (e.g., tenant ID). Null for global (context-independent) entries.
    ///     When invalidating, global entries and entries matching the current context are removed.
    /// </param>
    void RegisterCacheEntry(string cacheKey, IEnumerable<Type> entityTypes, string? contextKey = null);

    /// <summary>
    ///     Registers a cache entry with custom tags for later invalidation.
    /// </summary>
    /// <param name="cacheKey">The cache key to register.</param>
    /// <param name="tags">Tags for grouped invalidation.</param>
    /// <param name="contextKey">
    ///     The cache context key (e.g., tenant ID). Null for global (context-independent) entries.
    /// </param>
    void RegisterCacheEntry(string cacheKey, IEnumerable<string> tags, string? contextKey = null);

    /// <summary>
    ///     Invalidates cache entries by the keys specified via WithKey().
    ///     Automatically handles context prefixes (works for entries cached with or without IgnoreContext())
    ///     and suffix variants (:count, :any) produced by scalar terminal methods.
    ///     Also cleans up tracking dictionaries for the invalidated keys.
    /// </summary>
    /// <param name="keys">The cache keys as specified in WithKey() calls.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates cache entries by keys using an explicit context key.
    ///     Use this overload when there is no ambient context (e.g., in background services or event handlers).
    /// </summary>
    /// <param name="keys">The cache keys as specified in WithKey() calls.</param>
    /// <param name="contextKey">The context key (e.g., tenant ID) to scope invalidation to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateByKeysAsync(IEnumerable<string> keys, string contextKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears all cached entries across all providers.
    ///     Use with caution in production environments.
    /// </summary>
    Task ClearAllAsync(CancellationToken cancellationToken = default);
}
