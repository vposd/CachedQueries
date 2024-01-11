using CachedQueries.Core;
using CachedQueries.Core.Interfaces;

namespace CachedQueries.DependencyInjection;

/// <summary>
///     Cache options
/// </summary>
public class QueryCacheOptions
{
    private readonly  Dictionary<Type, Type> _servicesMap = new();
    public CacheOptions Options { get; internal set; } = new ()
    {
        DefaultExpiration = TimeSpan.FromHours(8),
        LockTimeout = TimeSpan.FromSeconds(5)
    };

    public QueryCacheOptions()
    {
        RegisterDefaultServices();
    }

    public IReadOnlyDictionary<Type, Type> GetServicesMap() => _servicesMap;

    /// <summary>
    ///     Setup a cache options
    /// </summary>
    /// <returns></returns>
    public QueryCacheOptions UseCacheOptions(CacheOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    ///     Setup a cache store
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheStore<T>() where T : class, ICacheStore
    {
        SetService(typeof(ICacheStore), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache store provider
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheStoreProvider<T>() where T : class, ICacheStoreProvider
    {
        SetService(typeof(ICacheStoreProvider), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheInvalidator<T>() where T : class, ICacheInvalidator
    {
        SetService(typeof(ICacheInvalidator), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseLockManager<T>() where T : class, ILockManager
    {
        SetService(typeof(ILockManager), typeof(T));
        return this;
    }

    /// <summary>
    ///     Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseKeyFactory<T>() where T : class, ICacheKeyFactory
    {
        SetService(typeof(ICacheKeyFactory), typeof(T));
        return this;
    }

    private void SetService(Type key, Type value)
    {
        _servicesMap.Remove(key);
        _servicesMap.Add(key, value);
    }
    
    private void RegisterDefaultServices()
    {
        UseKeyFactory<CacheKeyFactory>();
        UseCacheStoreProvider<CacheStoreProvider>();
        UseCacheInvalidator<DefaultCacheInvalidator>();
        UseLockManager<NullLockManager>();
    }
}
