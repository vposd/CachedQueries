using Lore.QueryCache.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.QueryCache.DependencyInjection;

public static class DependencyInjection
{
    /// <summary>
    /// Configure caching DI
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configOptions"></param>
    /// <returns></returns>
    public static IServiceCollection AddLoreCache(this IServiceCollection services,
        Action<LoreCacheOptions> configOptions)
    {
        var options = new LoreCacheOptions(services);
        configOptions(options);

        return services;
    }

    /// <summary>
    /// Use cache
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseLoreCache(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        var cache = serviceScope.ServiceProvider.GetRequiredService<ICache>();

        CacheManager.Cache = cache;
        return app;
    }
}