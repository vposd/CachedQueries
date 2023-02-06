using CachedQueries.Core;
using CachedQueries.Core.Interfaces;

namespace CachedQueries.DependencyInjection;

/// <summary>
///     Cache options
/// </summary>
public class QueryCacheOptions
{
    public readonly Dictionary<Type, Type> ServicesMap  = new();
    public CacheOptions Options { get; internal set; } = new ()
    {
        DefaultExpiration = TimeSpan.FromHours(8),
        LockTimeout = TimeSpan.FromSeconds(5)
    };

    public QueryCacheOptions()
    {
        Set(typeof(ICacheKeyFactory), typeof(CacheKeyFactory));
        Set(typeof(ICacheStoreProvider), typeof(CacheStoreProvider));
        Set(typeof(ICacheInvalidator), typeof(DefaultCacheInvalidator));
        Set(typeof(ILockManager), typeof(DefaultLockManager));
    }

    /// <summary>
    ///     Setup a cache options
    /// </summary>
    /// <returns></returns>
    public QueryCacheOptions UseCacheOptions(CacheOptions options)
    {
        Options = options;
        return this;
    }

    /// <summary>
    ///     Setup a cache store
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheStore<T>() where T : class, ICacheStore
    {
        Set(typeof(ICacheStore), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache store provider
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheStoreProvider<T>() where T : class, ICacheStoreProvider
    {
        Set(typeof(ICacheStoreProvider), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheInvalidator<T>() where T : class, ICacheInvalidator
    {
        Set(typeof(ICacheInvalidator), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseLockManager<T>() where T : class, ILockManager
    {
        Set(typeof(ILockManager), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseKeyFactory<T>() where T : class, ICacheKeyFactory
    {
        Set(typeof(ICacheKeyFactory), typeof(T));
        return this;
    }

    private void Set(Type key, Type value)
    {
        ServicesMap.Remove(key);
        ServicesMap.Add(key, value);
    }
}
