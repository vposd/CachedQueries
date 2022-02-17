using CachedQueries.Core;
using CachedQueries.Core.Interfaces;
using CachedQueries.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CachedQueries.DependencyInjection;

/// <summary>
/// Cache options
/// </summary>
public class LoreCacheOptions
{
    private readonly IServiceCollection _services;

    public LoreCacheOptions(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Setup a cache service
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public LoreCacheOptions UseCache<T>() where T : class, ICache
    {
        _services.AddSingleton<ICache, T>();
        return this;
    }

    /// <summary>
    /// Use Entity Framework workflow
    /// </summary>
    /// <returns></returns>
    public LoreCacheOptions UseEntityFramework()
    {
        CacheManager.CacheKeyFactory = new QueryCacheKeyFactory();
        return this;
    }
}