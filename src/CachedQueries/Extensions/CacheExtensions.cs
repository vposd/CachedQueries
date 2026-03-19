using CachedQueries.Abstractions;

namespace CachedQueries.Extensions;

/// <summary>
/// Extension methods for cache operations.
/// </summary>
public static class CacheExtensions
{
    /// <summary>
    /// Clears all cached entries across all providers.
    /// Use with caution in production environments.
    /// </summary>
    /// <example>
    /// await Cache.ClearAllAsync();
    /// </example>
    public static async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        if (!CacheServiceAccessor.IsConfigured)
        {
            throw new InvalidOperationException("CachedQueries is not configured. Call UseCachedQueries() first.");
        }

        await CacheServiceAccessor.Invalidator!.ClearAllAsync(cancellationToken);
    }

    /// <summary>
    /// Clears all cached entries for the current cache context (e.g., tenant).
    /// Only affects entries within the current context scope.
    /// </summary>
    /// <example>
    /// // Clear cache for current tenant
    /// await Cache.ClearContextAsync();
    /// </example>
    public static async Task ClearContextAsync(CancellationToken cancellationToken = default)
    {
        if (!CacheServiceAccessor.IsConfigured)
        {
            throw new InvalidOperationException("CachedQueries is not configured. Call UseCachedQueries() first.");
        }

        await CacheServiceAccessor.Invalidator!.ClearContextAsync(cancellationToken);
    }

    /// <summary>
    /// Invalidates cache entries by entity types.
    /// </summary>
    public static async Task InvalidateAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default)
    {
        if (!CacheServiceAccessor.IsConfigured)
        {
            throw new InvalidOperationException("CachedQueries is not configured. Call UseCachedQueries() first.");
        }

        await CacheServiceAccessor.Invalidator!.InvalidateAsync(entityTypes, cancellationToken);
    }

    /// <summary>
    /// Invalidates cache entries by entity type.
    /// </summary>
    public static Task InvalidateAsync<TEntity>(CancellationToken cancellationToken = default)
        => InvalidateAsync([typeof(TEntity)], cancellationToken);

    /// <summary>
    /// Invalidates cache entries by the keys specified via WithKey().
    /// Automatically handles context prefixes and suffix variants (:count, :any).
    /// </summary>
    public static async Task InvalidateByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (!CacheServiceAccessor.IsConfigured)
        {
            throw new InvalidOperationException("CachedQueries is not configured. Call UseCachedQueries() first.");
        }

        await CacheServiceAccessor.Invalidator!.InvalidateByKeysAsync(keys, cancellationToken);
    }

    /// <summary>
    /// Invalidates a cache entry by the key specified via WithKey().
    /// Automatically handles context prefixes and suffix variants (:count, :any).
    /// </summary>
    public static Task InvalidateByKeyAsync(string key, CancellationToken cancellationToken = default)
        => InvalidateByKeysAsync([key], cancellationToken);

    /// <summary>
    /// Invalidates cache entries by tags.
    /// </summary>
    public static async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        if (!CacheServiceAccessor.IsConfigured)
        {
            throw new InvalidOperationException("CachedQueries is not configured. Call UseCachedQueries() first.");
        }

        await CacheServiceAccessor.Invalidator!.InvalidateByTagsAsync(tags, cancellationToken);
    }

    /// <summary>
    /// Invalidates cache entries by a single tag.
    /// </summary>
    public static Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
        => InvalidateByTagsAsync([tag], cancellationToken);
}

/// <summary>
/// Static helper class for cache operations.
/// Provides a convenient API for cache management.
/// </summary>
public static class Cache
{
    /// <summary>
    /// Clears all cached entries across all providers.
    /// </summary>
    public static Task ClearAllAsync(CancellationToken cancellationToken = default)
        => CacheExtensions.ClearAllAsync(cancellationToken);

    /// <summary>
    /// Clears all cached entries for the current cache context (e.g., tenant).
    /// </summary>
    public static Task ClearContextAsync(CancellationToken cancellationToken = default)
        => CacheExtensions.ClearContextAsync(cancellationToken);

    /// <summary>
    /// Invalidates cache for the specified entity type.
    /// </summary>
    public static Task InvalidateAsync<TEntity>(CancellationToken cancellationToken = default)
        => CacheExtensions.InvalidateAsync<TEntity>(cancellationToken);

    /// <summary>
    /// Invalidates a cache entry by its exact key.
    /// </summary>
    public static Task InvalidateByKeyAsync(string key, CancellationToken cancellationToken = default)
        => CacheExtensions.InvalidateByKeyAsync(key, cancellationToken);

    /// <summary>
    /// Invalidates cache entries by their exact keys.
    /// </summary>
    public static Task InvalidateByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        => CacheExtensions.InvalidateByKeysAsync(keys, cancellationToken);

    /// <summary>
    /// Invalidates cache entries by tags.
    /// </summary>
    public static Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
        => CacheExtensions.InvalidateByTagAsync(tag, cancellationToken);

    /// <summary>
    /// Invalidates cache entries by multiple tags.
    /// </summary>
    public static Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        => CacheExtensions.InvalidateByTagsAsync(tags, cancellationToken);
}
