using CachedQueries.Core;
using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;
using CachedQueries.Core.Strategies;

namespace CachedQueries.DependencyInjection;

/// <summary>
/// Provides configuration options for setting up caching strategies, services, and behavior in CachedQueries.
/// </summary>
public class CachedQueriesOptions
{
    private readonly Dictionary<Type, Type> _servicesMap = new();

    /// <summary>
    /// Initializes a new instance of <see cref="CachedQueriesOptions"/> with default services for cache key factory,
    /// cache invalidator, collection strategy, entry strategy, and context provider.
    /// </summary>
    public CachedQueriesOptions()
    {
        RegisterDefaultServices();
    }

    /// <summary>
    /// Gets the current cache configuration options.
    /// </summary>
    public CachedQueriesConfig Options { get; private set; } = new()
    {
        DefaultCacheDuration = TimeSpan.FromHours(8)
    };

    /// <summary>
    /// Returns a read-only dictionary of the currently registered service types for caching components.
    /// </summary>
    /// <returns>An <see cref="IReadOnlyDictionary{Type, Type}"/> representing the registered service mappings.</returns>
    public IReadOnlyDictionary<Type, Type> GetServicesMap()
    {
        return _servicesMap;
    }

    /// <summary>
    /// Configures custom caching options.
    /// </summary>
    /// <param name="options">An instance of <see cref="CachedQueriesConfig"/> to configure cache behavior such as default cache duration.</param>
    /// <returns>The <see cref="CachedQueriesOptions"/> instance, allowing for fluent configuration chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="options"/> is null.</exception>
    public CachedQueriesOptions UseCachingOptions(CachedQueriesConfig options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    /// Registers a custom cache store implementation.
    /// </summary>
    /// <typeparam name="T">The type of the class implementing <see cref="ICacheStore"/>.</typeparam>
    /// <returns>The <see cref="CachedQueriesOptions"/> instance for fluent configuration chaining.</returns>
    public CachedQueriesOptions UseCacheStore<T>() where T : class, ICacheStore
    {
        SetService(typeof(ICacheStore), typeof(T));
        return this;
    }

    /// <summary>
    /// Registers a custom cache invalidator implementation.
    /// </summary>
    /// <typeparam name="T">The type of the class implementing <see cref="ICacheInvalidator"/>.</typeparam>
    /// <returns>The <see cref="CachedQueriesOptions"/> instance for fluent configuration chaining.</returns>
    public CachedQueriesOptions UseCacheInvalidator<T>() where T : class, ICacheInvalidator
    {
        SetService(typeof(ICacheInvalidator), typeof(T));
        return this;
    }

    /// <summary>
    /// Registers a custom cache key factory implementation to generate unique cache keys.
    /// </summary>
    /// <typeparam name="T">The type of the class implementing <see cref="ICacheKeyFactory"/>.</typeparam>
    /// <returns>The <see cref="CachedQueriesOptions"/> instance for fluent configuration chaining.</returns>
    public CachedQueriesOptions UseCacheKeyFactory<T>() where T : class, ICacheKeyFactory
    {
        SetService(typeof(ICacheKeyFactory), typeof(T));
        return this;
    }

    /// <summary>
    /// Registers a custom cache collection strategy to control how cache entries are grouped and collected.
    /// </summary>
    /// <typeparam name="T">The type of the class implementing <see cref="ICacheCollectionStrategy"/>.</typeparam>
    /// <returns>The <see cref="CachedQueriesOptions"/> instance for fluent configuration chaining.</returns>
    public CachedQueriesOptions UseCacheCollectionStrategy<T>() where T : class, ICacheCollectionStrategy
    {
        SetService(typeof(ICacheCollectionStrategy), typeof(T));
        return this;
    }

    /// <summary>
    /// Registers a custom cache entry strategy to control the behavior of individual cache entries.
    /// </summary>
    /// <typeparam name="T">The type of the class implementing <see cref="ICacheEntryStrategy"/>.</typeparam>
    /// <returns>The <see cref="CachedQueriesOptions"/> instance for fluent configuration chaining.</returns>
    public CachedQueriesOptions UseCacheEntryStrategy<T>() where T : class, ICacheEntryStrategy
    {
        SetService(typeof(ICacheEntryStrategy), typeof(T));
        return this;
    }
    
    /// <summary>
    /// Registers a custom cache context provider to manage cache-scoped data.
    /// </summary>
    /// <typeparam name="T">The type of the class implementing <see cref="ICacheContextProvider"/>.</typeparam>
    /// <returns>The <see cref="CachedQueriesOptions"/> instance for fluent configuration chaining.</returns>
    public CachedQueriesOptions UseCacheContextProvider<T>() where T : class, ICacheContextProvider
    {
        SetService(typeof(ICacheContextProvider), typeof(T));
        return this;
    }

    /// <summary>
    /// Adds or replaces the service mapping for a specific cache component.
    /// </summary>
    /// <param name="key">The service type interface (e.g., <see cref="ICacheStore"/>).</param>
    /// <param name="value">The implementation type to use for the service.</param>
    private void SetService(Type key, Type value)
    {
        _servicesMap.Remove(key);
        _servicesMap.Add(key, value);
    }

    /// <summary>
    /// Registers the default services for cache key factory, cache invalidator, cache collection strategy,
    /// cache entry strategy, and cache context provider.
    /// </summary>
    private void RegisterDefaultServices()
    {
        UseCacheKeyFactory<DefaultCacheKeyFactory>();
        UseCacheInvalidator<DefaultCacheInvalidator>();
        UseCacheCollectionStrategy<DefaultCacheCollectionStrategy>();
        UseCacheEntryStrategy<DefaultCacheEntryStrategy>();
        UseCacheContextProvider<DefaultCacheContextProvider>();
    }
}
