using CachedQueries.Core.Abstractions;

namespace CachedQueries.DependencyInjection;

/// <summary>
///     A static container for resolving instances of <see cref="ICacheManager" /> from the application's service provider.
///     This class manages the lifecycle of the cache manager by utilizing scoped services.
/// </summary>
public static class CacheManagerContainer
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    ///     Initializes the <see cref="CacheManagerContainer" /> with a given <see cref="IServiceProvider" />.
    ///     This method must be called before attempting to resolve the <see cref="ICacheManager" />.
    /// </summary>
    /// <param name="serviceProvider">An instance of <see cref="IServiceProvider" /> to resolve services from.</param>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    ///     Resolves an instance of <see cref="ICacheManager" /> from the configured service provider.
    ///     If the container has not been initialized, an exception is thrown.
    /// </summary>
    /// <returns>An instance of <see cref="ICacheManager" />.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if the container has not been initialized by calling
    ///     <see cref="Initialize" />.
    /// </exception>
    public static ICacheManager Resolve()
    {
        if (_serviceProvider is null)
        {
            throw new ArgumentException(
                "CacheManagerContainer is not initialized. Call Initialize() with a valid IServiceProvider.");
        }

        using var scope = _serviceProvider.CreateScope();
        var cacheManager = scope.ServiceProvider.GetRequiredService<ICacheManager>();
        return cacheManager;
    }

    /// <summary>
    ///     Resets the <see cref="CacheManagerContainer" /> by setting the internal service provider reference to null.
    ///     This effectively de-initializes the container.
    /// </summary>
    public static void Reset()
    {
        _serviceProvider = null;
    }
}
