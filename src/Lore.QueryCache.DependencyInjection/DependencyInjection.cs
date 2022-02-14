using Lore.QueryCache.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.QueryCache.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddLoreCache(this IServiceCollection services,
        Action<LoreCacheOptions> configOptions)
    {
        var options = new LoreCacheOptions(services);
        configOptions(options);

        return services;
    }

    public static IApplicationBuilder UseLoreCache(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        var cache = serviceScope.ServiceProvider.GetRequiredService<ICache>();

        CacheManager.Cache = cache;
        return app;
    }
}