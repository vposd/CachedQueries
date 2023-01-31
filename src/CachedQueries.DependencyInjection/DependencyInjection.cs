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

        services.AddScoped<ICacheManager, CacheManager>();

        return services;
    }

    /// <summary>
    ///     Use cache
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseQueriesCaching(this IApplicationBuilder app)
    {
        CacheManagerContainer.Initialize(app.ApplicationServices);
        return app;
    }
}
