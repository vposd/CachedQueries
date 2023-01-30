using CachedQueries.Core;
using CachedQueries.Core.Interfaces;
using CachedQueries.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CachedQueries.DependencyInjection;

/// <summary>
/// Cache options
/// </summary>
public class QueryCacheOptions
{
    private readonly IServiceCollection _services;

    public QueryCacheOptions(IServiceCollection services)
    {
        _services = services;
        UseKeyFactory<CacheKeyFactory>();
        UseCacheStoreProvider<CacheStoreProvider>();
        UseCacheInvalidator<DefaultCacheInvalidator>();
        UseLockManager<DefaultLockManager>();
        UseCacheOptions(new CacheOptions
        {
            DefaultExpiration = TimeSpan.FromHours(8),
            LockTimeout = TimeSpan.FromSeconds(5)
        });
    }

    /// <summary>
    /// Setup a cache store provider
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheOptions(CacheOptions options)
    {
        _services.AddSingleton(options);
        return this;
    }

    /// <summary>
    /// Setup a cache store
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheStore<T>() where T : class, ICacheStore
    {
        _services.AddScoped<ICacheStore, T>();

        return this;
    }

    /// <summary>
    /// Setup a cache store provider
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheStoreProvider<T>() where T : class, ICacheStoreProvider
    {
        _services.AddScoped<ICacheStoreProvider, T>();

        return this;
    }

    /// <summary>
    /// Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCacheInvalidator<T>() where T : class, ICacheInvalidator
    {
        _services.AddScoped<ICacheInvalidator, T>();
        return this;
    }

    /// <summary>
    /// Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseLockManager<T>() where T : class, ILockManager
    {
        _services.AddScoped<ILockManager, T>();
        return this;
    }

    /// <summary>
    /// Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseKeyFactory<T>() where T : class, ICacheKeyFactory
    {
        _services.AddScoped<ICacheKeyFactory, T>();
        return this;
    }

    /// <summary>
    /// Use Entity Framework workflow
    /// </summary>
    /// <returns></returns>
    public QueryCacheOptions UseEntityFramework()
    {
        UseKeyFactory<QueryCacheKeyFactory>();
        return this;
    }
}
