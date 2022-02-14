using Lore.QueryCache.EntityFramework;
using Lore.QueryCache.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.QueryCache.DependencyInjection;

public class LoreCacheOptions
{
    private readonly IServiceCollection _services;

    public LoreCacheOptions(IServiceCollection services)
    {
        _services = services;
    }

    public LoreCacheOptions UseCache<T>() where T : class, ICache
    {
        _services.AddSingleton<ICache, T>();
        return this;
    }

    public LoreCacheOptions UseEntityFramework()
    {
        CacheManager.CacheKeyFactory = new QueryCacheKeyFactory();
        return this;
    }
}