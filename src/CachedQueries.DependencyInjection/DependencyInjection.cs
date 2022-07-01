using CachedQueries.Core;
using CachedQueries.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CachedQueries.DependencyInjection;

public static class DependencyInjection
{
    /// <summary>
    ///     Configure caching DI
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configOptions"></param>
    /// <returns></returns>
    public static IServiceCollection AddQueriesCaching(this IServiceCollection services,
        Action<QueryCacheOptions> configOptions)
    {
        var options = new QueryCacheOptions(services);
        configOptions(options);

        return services;
    }

    /// <summary>
    ///     Use cache
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseQueriesCaching(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();

        var cache = serviceScope.ServiceProvider.GetRequiredService<ICache>();
        var cacheInvalidator = serviceScope.ServiceProvider.GetRequiredService<ICacheInvalidator>();
        var lockManager = serviceScope.ServiceProvider.GetRequiredService<ILockManager>();

        CacheManager.Cache = cache;
        CacheManager.CacheInvalidator = cacheInvalidator;
        CacheManager.LockManager = lockManager;

        return app;
    }
}
