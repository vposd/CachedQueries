using CachedQueries.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CachedQueries.DependencyInjection;

public static class CacheManagerContainer
{
    private static IServiceProvider? _serviceProvider;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static ICacheManager Resolve()
    {
        if (_serviceProvider is null)
            throw new ArgumentException("CacheManagerContainer is not initialized");

        using var scope = _serviceProvider.CreateScope();
        var cacheManager = scope.ServiceProvider.GetRequiredService<ICacheManager>();
        return cacheManager;
    }
}
