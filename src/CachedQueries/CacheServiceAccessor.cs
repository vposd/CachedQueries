using CachedQueries.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CachedQueries;

/// <summary>
///     Static accessor for cache services, used by <see cref="CacheableQuery{T}" /> and <c>Cache</c>.
///     Populated by <c>app.Services.UseCachedQueries()</c>.
/// </summary>
/// <remarks>
///     The accessor is process-wide: one configuration per process. Until it is configured,
///     <c>.Cacheable()</c> queries execute without caching and the <c>Cache</c> helpers throw.
/// </remarks>
public static class CacheServiceAccessor
{
    private static IServiceScopeFactory? _scopeFactory;

    internal static ICacheProvider? CacheProvider { get; private set; }
    internal static ICacheKeyGenerator? KeyGenerator { get; private set; }
    internal static ICacheInvalidator? Invalidator { get; private set; }
    internal static ICacheProviderFactory? ProviderFactory { get; private set; }
    internal static string CachePrefix { get; private set; } = "cq";
    internal static CachingOptions DefaultOptions { get; private set; } = CachingOptions.Default;

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
        _scopeFactory = serviceProvider.GetService<IServiceScopeFactory>();

        CacheProvider = serviceProvider.GetService<ICacheProvider>();
        KeyGenerator = serviceProvider.GetService<ICacheKeyGenerator>();
        Invalidator = serviceProvider.GetService<ICacheInvalidator>();
        ProviderFactory = serviceProvider.GetService<ICacheProviderFactory>();

        var config = serviceProvider.GetService<CachedQueriesConfiguration>();
        if (config is not null)
        {
            CachePrefix = config.CachePrefix;
            DefaultOptions = config.DefaultOptions;
        }
    }

    /// <summary>
    ///     Gets the current cache context key (e.g., tenant ID).
    /// </summary>
    /// <remarks>
    ///     The <see cref="ICacheContextProvider" /> is resolved from a <b>new</b> DI scope, not from the current
    ///     request scope. It must therefore read ambient state (e.g. <c>IHttpContextAccessor</c>, an
    ///     <c>AsyncLocal</c>) rather than state stored in other scoped services by middleware.
    /// </remarks>
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
        _scopeFactory = null;
        CacheProvider = null;
        KeyGenerator = null;
        Invalidator = null;
        ProviderFactory = null;
        CachePrefix = "cq";
        DefaultOptions = CachingOptions.Default;
    }
}
