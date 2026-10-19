using CachedQueries.Abstractions;
using CachedQueries.Interceptors;
using CachedQueries.Internal;
using CachedQueries.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Extensions;

/// <summary>
///     Extension methods for configuring CachedQueries in DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds CachedQueries services with in-memory cache as default provider.
    /// </summary>
    /// <example>
    ///     // Basic usage with in-memory cache
    ///     services.AddCachedQueries();
    ///     // With custom options
    ///     services.AddCachedQueries(config => {
    ///     config.DefaultOptions = new CachingOptions(TimeSpan.FromHours(1));
    ///     config.AutoInvalidation = true;
    ///     });
    ///     // With multiple providers
    ///     services.AddCachedQueries(config => {
    ///     config
    ///     .UseSingleItemProvider&lt;RedisCacheProvider&gt;()
    ///     .UseCollectionProvider&lt;MongoCacheProvider&gt;()
    ///     .UseScalarProvider&lt;RedisCacheProvider&gt;();
    ///     });
    /// </example>
    public static IServiceCollection AddCachedQueries(
        this IServiceCollection services,
        Action<CachedQueriesConfiguration>? configure = null)
    {
        var configuration = new CachedQueriesConfiguration();
        configure?.Invoke(configuration);

        services.AddSingleton(configuration);
        services.AddMemoryCache();

        services.AddSingleton<MemoryCacheProvider>();
        services.AddSingleton<ICacheProvider>(sp => sp.GetRequiredService<MemoryCacheProvider>());

        RegisterCoreServices(services, configuration);

        return services;
    }

    /// <summary>
    ///     Adds CachedQueries services with custom default cache provider.
    /// </summary>
    /// <example>
    ///     // Using Redis as default provider
    ///     services.AddCachedQueries&lt;RedisCacheProvider&gt;();
    ///     // With configuration
    ///     services.AddCachedQueries&lt;RedisCacheProvider&gt;(config => {
    ///     config.DefaultOptions = new CachingOptions(TimeSpan.FromMinutes(15));
    ///     });
    /// </example>
    public static IServiceCollection AddCachedQueries<TDefaultProvider>(
        this IServiceCollection services,
        Action<CachedQueriesConfiguration>? configure = null)
        where TDefaultProvider : class, ICacheProvider
    {
        var configuration = new CachedQueriesConfiguration();
        configure?.Invoke(configuration);

        services.AddSingleton(configuration);
        services.AddSingleton<TDefaultProvider>();
        services.AddSingleton<ICacheProvider>(sp => sp.GetRequiredService<TDefaultProvider>());

        RegisterCoreServices(services, configuration);

        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services, CachedQueriesConfiguration configuration)
    {
        // Register context provider if configured
        if (configuration.ContextProviderType is not null)
        {
            services.AddScoped(typeof(ICacheContextProvider), configuration.ContextProviderType);
        }

        services.AddSingleton<ICacheKeyGenerator, QueryCacheKeyGenerator>();
        services.AddSingleton<CacheInvalidationInterceptor>();
        services.AddSingleton<TransactionCacheInvalidationInterceptor>();

        // Register provider factory
        services.AddSingleton<ICacheProviderFactory>(sp =>
        {
            if (configuration.ProviderFactory is not null)
            {
                return configuration.ProviderFactory(sp);
            }

            var defaultProvider = sp.GetRequiredService<ICacheProvider>();

            var singleProvider = configuration.SingleItemProviderType is not null
                ? (ICacheProvider)sp.GetRequiredService(configuration.SingleItemProviderType)
                : null;

            var collectionProvider = configuration.CollectionProviderType is not null
                ? (ICacheProvider)sp.GetRequiredService(configuration.CollectionProviderType)
                : null;

            var scalarProvider = configuration.ScalarProviderType is not null
                ? (ICacheProvider)sp.GetRequiredService(configuration.ScalarProviderType)
                : null;

            return new CacheProviderFactory(defaultProvider, singleProvider, collectionProvider, scalarProvider);
        });

        // Register invalidator with provider factory
        // Note: IServiceScopeFactory is passed to resolve scoped ICacheContextProvider at runtime
        services.AddSingleton<ICacheInvalidator>(sp =>
        {
            var cacheProvider = sp.GetRequiredService<ICacheProvider>();
            var providerFactory = sp.GetRequiredService<ICacheProviderFactory>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<ILogger<CacheInvalidator>>();
            return new CacheInvalidator(cacheProvider, providerFactory, scopeFactory, logger);
        });
    }

    /// <summary>
    ///     Registers a cache context provider for multi-tenant or scoped caching.
    /// </summary>
    /// <example>
    ///     services.AddCacheContextProvider&lt;TenantCacheContextProvider&gt;();
    /// </example>
    public static IServiceCollection AddCacheContextProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ICacheContextProvider
    {
        services.AddScoped<ICacheContextProvider, TProvider>();
        return services;
    }

    /// <summary>
    ///     Registers a cache context provider using a factory function.
    /// </summary>
    /// <example>
    ///     services.AddCacheContextProvider(sp => new TenantCacheContextProvider(sp.GetRequiredService&lt;ITenantAccessor&gt;
    ///     ()));
    /// </example>
    public static IServiceCollection AddCacheContextProvider(
        this IServiceCollection services,
        Func<IServiceProvider, ICacheContextProvider> factory)
    {
        services.AddScoped(factory);
        return services;
    }

    /// <summary>
    ///     Initializes the CachedQueries static accessor.
    ///     Call this in your application startup after building the service provider.
    /// </summary>
    public static IServiceProvider UseCachedQueries(this IServiceProvider serviceProvider)
    {
        CacheServiceAccessor.Configure(serviceProvider);
        return serviceProvider;
    }

    /// <summary>
    ///     Adds cache invalidation interceptors to the options of an already registered DbContext.
    ///     Must be called <b>after</b> <c>AddDbContext</c> / <c>AddDbContextPool</c> / <c>AddDbContextFactory</c>.
    /// </summary>
    /// <remarks>
    ///     When <see cref="CachedQueriesConfiguration.AutoInvalidation" /> is false, the options are left unchanged
    ///     and cache entries are only invalidated manually (via <c>Cache</c>) or by expiration.
    /// </remarks>
    /// <typeparam name="TContext">The DbContext type to configure.</typeparam>
    /// <exception cref="InvalidOperationException">No <c>DbContextOptions&lt;TContext&gt;</c> is registered yet.</exception>
    /// <example>
    ///     services.AddDbContext&lt;MyDbContext&gt;(options => options.UseSqlServer(connectionString));
    ///     services.AddCacheInvalidation&lt;MyDbContext&gt;();
    /// </example>
    public static IServiceCollection AddCacheInvalidation<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        var existingDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(DbContextOptions<TContext>))
                                 ?? throw new InvalidOperationException(
                                     $"No DbContextOptions<{typeof(TContext).Name}> registration found. " +
                                     $"Call AddDbContext<{typeof(TContext).Name}>() before AddCacheInvalidation<{typeof(TContext).Name}>().");

        services.Remove(existingDescriptor);
        services.Add(ServiceDescriptor.Describe(
            typeof(DbContextOptions<TContext>),
            sp => CreateDecoratedOptions<TContext>(existingDescriptor, sp),
            existingDescriptor.Lifetime));

        return services;
    }

    private static DbContextOptions<TContext> CreateDecoratedOptions<TContext>(
        ServiceDescriptor originalDescriptor,
        IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        var originalOptions = (DbContextOptions<TContext>)(
            originalDescriptor.ImplementationInstance
            ?? originalDescriptor.ImplementationFactory?.Invoke(serviceProvider)
            ?? ActivatorUtilities.CreateInstance(serviceProvider, originalDescriptor.ImplementationType!));

        var builder = new DbContextOptionsBuilder<TContext>(originalOptions);
        builder.AddCacheInvalidation(serviceProvider);
        return builder.Options;
    }
}
