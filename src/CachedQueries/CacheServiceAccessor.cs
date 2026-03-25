using CachedQueries.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CachedQueries;

/// <summary>
///     Static accessor for cache services.
///     Configured during DI setup.
/// </summary>
public static class CacheServiceAccessor
{
    private static IServiceProvider? _serviceProvider;
    private static IServiceScopeFactory? _scopeFactory;

    internal static ICacheProvider? CacheProvider { get; private set; }
    internal static ICacheKeyGenerator? KeyGenerator { get; private set; }
    internal static ICacheInvalidator? Invalidator { get; private set; }
    internal static ICacheProviderFactory? ProviderFactory { get; private set; }
    internal static string CachePrefix { get; private set; } = "cq";

    /// <summary>
    ///     Gets whether the cache services are configured.
    /// </summary>
    public static bool IsConfigured => CacheProvider is not null && KeyGenerator is not null && Invalidator is not null;

    /// <summary>
    ///     Configures the cache services. Called automatically by DI setup.
    /// </summary>
    public static void Configure(
        ICacheProvider cacheProvider,
        ICacheKeyGenerator keyGenerator,
        ICacheInvalidator invalidator,
        ICacheProviderFactory? providerFactory = null)
    {
        CacheProvider = cacheProvider;
        KeyGenerator = keyGenerator;
        Invalidator = invalidator;
        ProviderFactory = providerFactory;
    }

    /// <summary>
    ///     Configures the service provider for resolving services.
    /// </summary>
    public static void Configure(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _scopeFactory = serviceProvider.GetService<IServiceScopeFactory>();

        CacheProvider = serviceProvider.GetService<ICacheProvider>();
        KeyGenerator = serviceProvider.GetService<ICacheKeyGenerator>();
        Invalidator = serviceProvider.GetService<ICacheInvalidator>();
        ProviderFactory = serviceProvider.GetService<ICacheProviderFactory>();

        var config = serviceProvider.GetService<CachedQueriesConfiguration>();
        if (config is not null)
        {
            CachePrefix = config.CachePrefix;
        }
    }

    /// <summary>
    ///     Gets the current cache context key (e.g., tenant ID).
    ///     Creates a scope to properly resolve scoped ICacheContextProvider.
    /// </summary>
    public static string? GetContextKey()
    {
        if (_scopeFactory is null)
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var contextProvider = scope.ServiceProvider.GetService<ICacheContextProvider>();
        return contextProvider?.GetContextKey();
    }

    /// <summary>
    ///     Resets the configuration. Used for testing.
    /// </summary>
    public static void Reset()
    {
        _serviceProvider = null;
        _scopeFactory = null;
        CacheProvider = null;
        KeyGenerator = null;
        Invalidator = null;
        ProviderFactory = null;
        CachePrefix = "cq";
    }
}
