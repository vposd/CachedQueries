using CachedQueries.Core;
using CachedQueries.Core.Abstractions;

namespace CachedQueries.DependencyInjection;

/// <summary>
///     Provides extension methods for configuring caching services and integrating CachedQueries into the application's
///     dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    ///     Configures caching services and strategies for the application by registering the required dependencies into the
    ///     service collection.
    /// </summary>
    /// <param name="services">The application's <see cref="IServiceCollection" /> to add caching services to.</param>
    /// <param name="configOptions">An action to configure cache options such as cache stores, invalidators, and key factories.</param>
    /// <returns>The updated <see cref="IServiceCollection" /> with caching services registered.</returns>
    public static IServiceCollection AddCachedQueries(this IServiceCollection services,
        Action<CachedQueriesOptions> configOptions)
    {
        // Create a new instance of CachedQueriesOptions and apply the provided configuration
        var options = new CachedQueriesOptions();
        configOptions(options);

        // Register each service in the service map with a scoped lifetime
        foreach (var (key, value) in options.GetServicesMap())
        {
            services.AddScoped(key, value);
        }

        // Register caching options and the cache manager
        services.AddSingleton(options.Options);
        services.AddScoped<ICacheManager, CacheManager>();

        return services;
    }

    /// <summary>
    ///     Initializes the caching system by configuring the <see cref="CacheManagerContainer" /> with the application's
    ///     service provider.
    ///     This method should be called during the application's startup to enable query caching.
    /// </summary>
    /// <param name="app">The application's <see cref="IApplicationBuilder" /> used to access the service provider.</param>
    /// <returns>The updated <see cref="IApplicationBuilder" />.</returns>
    public static IApplicationBuilder UseCachedQueries(this IApplicationBuilder app)
    {
        // Initialize the CacheManagerContainer with the application's service provider
        CacheManagerContainer.Initialize(app.ApplicationServices);
        return app;
    }
}
