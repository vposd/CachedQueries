using System.Diagnostics.CodeAnalysis;
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
        // Note: IServiceProvider is passed to resolve scoped ICacheContextProvider at runtime
        services.AddSingleton<ICacheInvalidator>(sp =>
        {
            var cacheProvider = sp.GetRequiredService<ICacheProvider>();
            var providerFactory = sp.GetRequiredService<ICacheProviderFactory>();
            var logger = sp.GetRequiredService<ILogger<CacheInvalidator>>();
            return new CacheInvalidator(cacheProvider, providerFactory, sp, logger);
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
    ///     Adds cache invalidation interceptors to DbContext options.
    ///     Use this when you don't have direct access to DbContextOptionsBuilder.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to configure.</typeparam>
    /// <example>
    ///     // After registering your DbContext
    ///     services.AddDbContext&lt;MyDbContext&gt;(options => options.UseSqlServer(connectionString));
    ///     // Add cache invalidation interceptors
    ///     services.AddCacheInvalidation&lt;MyDbContext&gt;();
    /// </example>
    public static IServiceCollection AddCacheInvalidation<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        // Find and remove existing DbContextOptions<TContext> registration
        var existingDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(DbContextOptions<TContext>));

        if (existingDescriptor is not null)
        {
            services.Remove(existingDescriptor);

            // Re-register with interceptors
            services.Add(ServiceDescriptor.Describe(
                typeof(DbContextOptions<TContext>),
                sp =>
                {
                    var options = CreateDecoratedOptions<TContext>(existingDescriptor, sp);
                    return options;
                },
                existingDescriptor.Lifetime));
        }

        return services;
    }

    private static DbContextOptions<TContext> CreateDecoratedOptions<TContext>(
        ServiceDescriptor originalDescriptor,
        IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        // Get original options
        DbContextOptions<TContext> originalOptions;

        if (originalDescriptor.ImplementationInstance is not null)
        {
            originalOptions = (DbContextOptions<TContext>)originalDescriptor.ImplementationInstance;
        }
        else if (originalDescriptor.ImplementationFactory is not null)
        {
            originalOptions = (DbContextOptions<TContext>)originalDescriptor.ImplementationFactory(serviceProvider);
        }
        else
        {
            // This branch is only hit when DbContextOptions is registered via ImplementationType,
            // which doesn't happen with standard AddDbContext registration patterns.
            [ExcludeFromCodeCoverage]
            static DbContextOptions<TContext> CreateFromType(ServiceDescriptor desc, IServiceProvider sp)
            {
                return (DbContextOptions<TContext>)ActivatorUtilities.CreateInstance(sp, desc.ImplementationType!);
            }

            originalOptions = CreateFromType(originalDescriptor, serviceProvider);
        }

        // Create new options with interceptors
        var builder = new DbContextOptionsBuilder<TContext>(originalOptions);

        var saveChangesInterceptor = serviceProvider.GetService<CacheInvalidationInterceptor>();
        var transactionInterceptor = serviceProvider.GetService<TransactionCacheInvalidationInterceptor>();

        if (saveChangesInterceptor is not null)
        {
            builder.AddInterceptors(saveChangesInterceptor);
        }

        if (transactionInterceptor is not null)
        {
            builder.AddInterceptors(transactionInterceptor);
        }

        return builder.Options;
    }
}
