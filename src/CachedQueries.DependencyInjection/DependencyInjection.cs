using CachedQueries.Core;
using CachedQueries.Core.Abstractions;

namespace CachedQueries.DependencyInjection;

public static class DependencyInjection
{
    /// <summary>
    ///     Configure caching DI
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configOptions"></param>
    /// <returns></returns>
    public static IServiceCollection AddCachedQueries(this IServiceCollection services,
        Action<CachedQueriesOptions> configOptions)
    {
        var options = new CachedQueriesOptions();
        configOptions(options);

        foreach (var (key, value) in options.GetServicesMap())
        {
            services.AddScoped(key, value);
        }

        services.AddSingleton(options.Options);
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
