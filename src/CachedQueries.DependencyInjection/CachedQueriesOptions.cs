using CachedQueries.Core;
using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;
using CachedQueries.Core.Strategies;

namespace CachedQueries.DependencyInjection;

/// <summary>
///     Cache options
/// </summary>
public class CachedQueriesOptions
{
    private readonly Dictionary<Type, Type> _servicesMap = new();

    public CachedQueriesOptions()
    {
        RegisterDefaultServices();
    }

    public CachedQueriesConfig Options { get; private set; } = new()
    {
        DefaultCacheDuration = TimeSpan.FromHours(8)
    };

    public IReadOnlyDictionary<Type, Type> GetServicesMap()
    {
        return _servicesMap;
    }

    /// <summary>
    ///     Setup a cache options
    /// </summary>
    /// <returns></returns>
    public CachedQueriesOptions UseCachingOptions(CachedQueriesConfig options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    ///     Set up a cache store
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public CachedQueriesOptions UseCacheStore<T>() where T : class, ICacheStore
    {
        SetService(typeof(ICacheStore), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public CachedQueriesOptions UseCacheInvalidator<T>() where T : class, ICacheInvalidator
    {
        SetService(typeof(ICacheInvalidator), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public CachedQueriesOptions UseCacheKeyFactory<T>() where T : class, ICacheKeyFactory
    {
        SetService(typeof(ICacheKeyFactory), typeof(T));
        return this;
    }

    /// <summary>
    ///     Set a cache collection strategy
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public CachedQueriesOptions UseCacheCollectionStrategy<T>() where T : class, ICacheCollectionStrategy
    {
        SetService(typeof(ICacheCollectionStrategy), typeof(T));
        return this;
    }

    /// <summary>
    ///     Set a cache entry strategy
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public CachedQueriesOptions UseCacheEntryStrategy<T>() where T : class, ICacheEntryStrategy
    {
        SetService(typeof(ICacheEntryStrategy), typeof(T));
        return this;
    }

    private void SetService(Type key, Type value)
    {
        _servicesMap.Remove(key);
        _servicesMap.Add(key, value);
    }

    private void RegisterDefaultServices()
    {
        UseCacheKeyFactory<DefaultCacheKeyFactory>();
        UseCacheInvalidator<DefaultCacheInvalidator>();
        UseCacheCollectionStrategy<DefaultCacheCollectionStrategy>();
        UseCacheEntryStrategy<DefaultCacheEntryStrategy>();
    }
}
