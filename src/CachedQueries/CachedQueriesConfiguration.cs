using CachedQueries.Abstractions;

namespace CachedQueries;

/// <summary>
/// Configuration options for CachedQueries library.
/// </summary>
public sealed class CachedQueriesConfiguration
{
    /// <summary>
    /// Default caching options applied when no options are specified.
    /// </summary>
    public CachingOptions DefaultOptions { get; set; } = CachingOptions.Default;

    /// <summary>
    /// Whether to automatically invalidate cache when SaveChanges is called.
    /// Default is true.
    /// </summary>
    public bool AutoInvalidation { get; set; } = true;

    /// <summary>
    /// Whether to log cache operations (hits, misses, invalidations).
    /// Default is true.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Custom provider factory. If set, overrides individual provider settings.
    /// </summary>
    public Func<IServiceProvider, ICacheProviderFactory>? ProviderFactory { get; set; }

    internal Type? ContextProviderType { get; set; }
    internal Type? SingleItemProviderType { get; set; }
    internal Type? CollectionProviderType { get; set; }
    internal Type? ScalarProviderType { get; set; }

    /// <summary>
    /// Sets the cache provider for single items (FirstOrDefault, SingleOrDefault).
    /// </summary>
    public CachedQueriesConfiguration UseSingleItemProvider<TProvider>() where TProvider : class, ICacheProvider
    {
        SingleItemProviderType = typeof(TProvider);
        return this;
    }

    /// <summary>
    /// Sets the cache provider for collections (ToList, ToArray).
    /// </summary>
    public CachedQueriesConfiguration UseCollectionProvider<TProvider>() where TProvider : class, ICacheProvider
    {
        CollectionProviderType = typeof(TProvider);
        return this;
    }

    /// <summary>
    /// Sets the cache provider for scalar values (Count, Any, Sum).
    /// </summary>
    public CachedQueriesConfiguration UseScalarProvider<TProvider>() where TProvider : class, ICacheProvider
    {
        ScalarProviderType = typeof(TProvider);
        return this;
    }

    /// <summary>
    /// Sets the cache context provider for multi-tenant or scoped caching.
    /// The context provider determines cache isolation boundaries (e.g., per tenant).
    /// </summary>
    /// <example>
    /// services.AddCachedQueries(config => config
    ///     .UseContextProvider&lt;TenantCacheContextProvider&gt;()
    ///     .UseProvider&lt;RedisCacheProvider&gt;());
    /// </example>
    public CachedQueriesConfiguration UseContextProvider<TProvider>() where TProvider : class, ICacheContextProvider
    {
        ContextProviderType = typeof(TProvider);
        return this;
    }

    /// <summary>
    /// Sets the same provider for all cache targets.
    /// </summary>
    public CachedQueriesConfiguration UseProvider<TProvider>() where TProvider : class, ICacheProvider
    {
        SingleItemProviderType = typeof(TProvider);
        CollectionProviderType = typeof(TProvider);
        ScalarProviderType = typeof(TProvider);
        return this;
    }
}
