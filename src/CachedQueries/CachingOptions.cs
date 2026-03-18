namespace CachedQueries;

/// <summary>
/// Configuration options for caching behavior.
/// </summary>
public sealed class CachingOptions
{
    /// <summary>
    /// Default caching options with 30 minutes expiration.
    /// </summary>
    public static CachingOptions Default { get; } = new();

    /// <summary>
    /// Cache expiration time. Default is 30 minutes.
    /// </summary>
    public TimeSpan Expiration { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Whether to use sliding expiration. Default is false (absolute expiration).
    /// </summary>
    public bool UseSlidingExpiration { get; init; }

    /// <summary>
    /// Custom cache key. If null, key is generated from query expression.
    /// </summary>
    public string? CacheKey { get; init; }

    /// <summary>
    /// Tags for cache invalidation. When any entity with these tags changes, cache is invalidated.
    /// </summary>
    public IReadOnlyCollection<string> Tags { get; init; } = [];

    /// <summary>
    /// Whether to skip caching for this query. Useful for conditional caching.
    /// </summary>
    public bool SkipCache { get; init; }

    /// <summary>
    /// Whether to ignore the cache context (e.g., tenant isolation) for this query.
    /// When true, the cached entry is stored and retrieved globally, shared across all contexts.
    /// Useful for reference data like lookup tables or categories that are tenant-independent.
    /// </summary>
    public bool IgnoreContext { get; init; }

    /// <summary>
    /// Target cache type. Used to select appropriate cache provider.
    /// Default is Auto (determined by query type).
    /// </summary>
    public CacheTarget Target { get; init; } = CacheTarget.Auto;

    public CachingOptions()
    {
    }

    public CachingOptions(TimeSpan expiration)
    {
        Expiration = expiration;
    }

    public CachingOptions(TimeSpan expiration, bool useSlidingExpiration)
    {
        Expiration = expiration;
        UseSlidingExpiration = useSlidingExpiration;
    }

    public CachingOptions(string cacheKey, TimeSpan expiration)
    {
        CacheKey = cacheKey;
        Expiration = expiration;
    }
}



