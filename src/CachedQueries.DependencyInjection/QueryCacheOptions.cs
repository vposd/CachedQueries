using CachedQueries.Core;
using CachedQueries.Core.Interfaces;
using CachedQueries.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CachedQueries.DependencyInjection;

/// <summary>
///     Cache options
/// </summary>
public class QueryCacheOptions
{
    private readonly IServiceCollection _services;

    public QueryCacheOptions(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Setup a cache invalidator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public QueryCacheOptions UseCache<T>() where T : class, ICache
    {
        _services.AddSingleton<ICache, T>();
        _services.AddSingleton<ICacheInvalidator, DefaultCacheInvalidator>();

        return this;
    }

    /// <summary>
    /// Use Entity Framework workflow
    /// </summary>
    /// <returns></returns>
    public QueryCacheOptions UseEntityFramework()
    {
        CacheManager.CacheKeyFactory = new QueryCacheKeyFactory();
        return this;
    }
}
