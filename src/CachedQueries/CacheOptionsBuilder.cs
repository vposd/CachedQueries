namespace CachedQueries;

/// <summary>
/// Fluent builder for configuring cache options.
/// Used with the <see cref="Extensions.CacheableExtensions.Cacheable{T}(IQueryable{T}, Action{CacheOptionsBuilder})"/> method.
/// </summary>
/// <example>
/// await query.Cacheable(o => o
///     .Expire(TimeSpan.FromMinutes(5))
///     .WithTags("orders", "reports"))
///     .ToListAsync();
/// </example>
public sealed class CacheOptionsBuilder
{
    private TimeSpan _expiration = TimeSpan.FromMinutes(30);
    private bool _slidingExpiration;
    private string? _cacheKey;
    private readonly List<string> _tags = [];
    private bool _skipCache;
    private bool _ignoreContext;
    private CacheTarget _target = CacheTarget.Auto;

    /// <summary>
    /// Sets absolute expiration time for the cached entry.
    /// </summary>
    /// <param name="expiration">Time after which the cache entry expires.</param>
    public CacheOptionsBuilder Expire(TimeSpan expiration)
    {
        _expiration = expiration;
        _slidingExpiration = false;
        return this;
    }

    /// <summary>
    /// Sets sliding expiration time for the cached entry.
    /// The entry expires if not accessed within the specified duration.
    /// </summary>
    /// <param name="expiration">Sliding window duration.</param>
    public CacheOptionsBuilder SlidingExpiration(TimeSpan expiration)
    {
        _expiration = expiration;
        _slidingExpiration = true;
        return this;
    }

    /// <summary>
    /// Sets a custom cache key. If not set, the key is generated from the query expression.
    /// </summary>
    public CacheOptionsBuilder WithKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _cacheKey = key;
        return this;
    }

    /// <summary>
    /// Adds tags for grouped cache invalidation.
    /// </summary>
    public CacheOptionsBuilder WithTags(params string[] tags)
    {
        _tags.AddRange(tags);
        return this;
    }

    /// <summary>
    /// Conditionally skips caching. When true, the query executes normally without caching.
    /// </summary>
    public CacheOptionsBuilder SkipIf(bool condition)
    {
        _skipCache = condition;
        return this;
    }

    /// <summary>
    /// Ignores the cache context (e.g., tenant isolation) for this query.
    /// The cached entry will be stored and retrieved globally, shared across all contexts.
    /// Useful for reference data like lookup tables or categories that are tenant-independent.
    /// </summary>
    /// <example>
    /// var categories = await _context.Categories
    ///     .Cacheable(o => o.IgnoreContext())
    ///     .ToListAsync();
    /// </example>
    public CacheOptionsBuilder IgnoreContext()
    {
        _ignoreContext = true;
        return this;
    }

    /// <summary>
    /// Overrides the cache target (provider selection). Default is Auto.
    /// </summary>
    public CacheOptionsBuilder UseTarget(CacheTarget target)
    {
        _target = target;
        return this;
    }

    internal CachingOptions Build() => new()
    {
        Expiration = _expiration,
        UseSlidingExpiration = _slidingExpiration,
        CacheKey = _cacheKey,
        Tags = _tags.AsReadOnly(),
        SkipCache = _skipCache,
        IgnoreContext = _ignoreContext,
        Target = _target
    };
}
