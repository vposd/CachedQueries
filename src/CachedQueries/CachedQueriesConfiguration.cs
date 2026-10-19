using CachedQueries.Abstractions;

namespace CachedQueries;

/// <summary>
///     Configuration options for CachedQueries library.
/// </summary>
public sealed class CachedQueriesConfiguration
{
    /// <summary>
    ///     Cache key namespace prefix. Appears right after the Redis InstanceName
    ///     and before the context key / hash in every cache key and tag key.
    ///     Default is "cq".
    /// </summary>
    /// <example>
    ///     Key format: {InstanceName}{CachePrefix}:{contextKey}:{hash}
    ///     e.g. CacheLocal:cq:tenant-id:A1B2C3...
    /// </example>
    public string CachePrefix { get; set; } = "cq";

    /// <summary>
    ///     Default expiration for <c>.Cacheable()</c> and <c>.Cacheable(o =&gt; ...)</c>.
    ///     Only <see cref="CachingOptions.Expiration" /> and <see cref="CachingOptions.UseSlidingExpiration" /> are
    ///     used; options passed explicitly via <c>.Cacheable(CachingOptions)</c> are used as-is.
    ///     Default is 30 minutes absolute expiration.
    /// </summary>
    public CachingOptions DefaultOptions { get; set; } = CachingOptions.Default;

    /// <summary>
    ///     Whether to automatically invalidate cache when SaveChanges is called.
    ///     When false, <c>AddCacheInvalidation</c> does not register the EF interceptors and entries are only
    ///     invalidated manually (via <c>Cache</c>) or by expiration.
    ///     Default is true.
    /// </summary>
    public bool AutoInvalidation { get; set; } = true;

    /// <summary>
    ///     Has no effect. Control CachedQueries logging with standard logging configuration instead, e.g.
    ///     <c>"Logging:LogLevel:CachedQueries": "Warning"</c>.
    /// </summary>
    [Obsolete("Has no effect. Configure log levels for the \"CachedQueries\" category through logging configuration instead.")]
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    ///     Custom provider factory. If set, overrides <see cref="UseSingleItemProvider{TProvider}" />,
    ///     <see cref="UseCollectionProvider{TProvider}" />, <see cref="UseScalarProvider{TProvider}" /> and
    ///     <see cref="UseProvider{TProvider}" />.
    /// </summary>
    public Func<IServiceProvider, ICacheProviderFactory>? ProviderFactory { get; set; }

    internal Type? ContextProviderType { get; set; }
    internal Type? SingleItemProviderType { get; set; }
    internal Type? CollectionProviderType { get; set; }
    internal Type? ScalarProviderType { get; set; }

    /// <summary>
    ///     Sets the cache provider for single items (FirstOrDefault, SingleOrDefault).
    /// </summary>
    public CachedQueriesConfiguration UseSingleItemProvider<TProvider>() where TProvider : class, ICacheProvider
    {
        SingleItemProviderType = typeof(TProvider);
        return this;
    }

    /// <summary>
    ///     Sets the cache provider for collections (ToList).
    /// </summary>
    public CachedQueriesConfiguration UseCollectionProvider<TProvider>() where TProvider : class, ICacheProvider
    {
        CollectionProviderType = typeof(TProvider);
        return this;
    }

    /// <summary>
    ///     Sets the cache provider for scalar values (Count, Any).
    /// </summary>
    public CachedQueriesConfiguration UseScalarProvider<TProvider>() where TProvider : class, ICacheProvider
    {
        ScalarProviderType = typeof(TProvider);
        return this;
    }

    /// <summary>
    ///     Sets the cache context provider for multi-tenant or scoped caching.
    ///     The context provider determines cache isolation boundaries (e.g., per tenant).
    /// </summary>
    /// <example>
    ///     services.AddCachedQueries(config => config
    ///     .UseContextProvider&lt;TenantCacheContextProvider&gt;()
    ///     .UseProvider&lt;RedisCacheProvider&gt;());
    /// </example>
    public CachedQueriesConfiguration UseContextProvider<TProvider>() where TProvider : class, ICacheContextProvider
    {
        ContextProviderType = typeof(TProvider);
        return this;
    }

    /// <summary>
    ///     Sets the same provider for all cache targets.
    ///     The provider type must be registered in DI (e.g. via <c>AddCachedQueries&lt;TProvider&gt;()</c>).
    /// </summary>
    public CachedQueriesConfiguration UseProvider<TProvider>() where TProvider : class, ICacheProvider
    {
        SingleItemProviderType = typeof(TProvider);
        CollectionProviderType = typeof(TProvider);
        ScalarProviderType = typeof(TProvider);
        return this;
    }
}
