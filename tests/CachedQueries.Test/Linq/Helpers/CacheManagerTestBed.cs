using CachedQueries.Core.Cache;
using CachedQueries.DependencyInjection;
using CachedQueries.EntityFramework.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MemoryCache = CachedQueries.Core.Cache.MemoryCache;

namespace CachedQueries.Test.Linq.Helpers;

public static class CacheManagerTestBed
{
    public static IServiceCollection InitCacheManager(Type? cacheStoreType, bool initEmptyCacheKeyFactory = false)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        services.AddCachedQueries(options =>
        {
            if (cacheStoreType == typeof(MemoryCache))
            {
                options.UseCacheStore<MemoryCache>();
            }

            if (cacheStoreType == typeof(DistributedCache))
            {
                options.UseCacheStore<DistributedCache>();
            }

            options.UseEntityFramework();
            if (initEmptyCacheKeyFactory)
            {
                options.UseCacheKeyFactory<EmptyKeyCacheFactory>();
            }
        });

        var serviceProvider = services.BuildServiceProvider();
        CacheManagerContainer.Initialize(serviceProvider);

        return services;
    }

    public static Mock<IDistributedCache> ConfigureFailingDistributedCache(string method)
    {
        var cache = new Mock<IDistributedCache>();

        switch (method)
        {
            case "Get":
                cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception(""));
                break;
            case "Remove":
                cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception(""));
                break;
            case "Set":
                cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
                        It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception(""));
                break;
        }

        return cache;
    }

    public static Mock<IMemoryCache> ConfigureFailingMemoryCache(string method)
    {
        var cache = new Mock<IMemoryCache>();
        object expectedValue;
        switch (method)
        {
            case "Get":
                cache.Setup(x => x.TryGetValue(It.IsAny<object>(), out expectedValue!))
                    .Throws(new Exception(""));
                break;
            case "Remove":
                cache.Setup(x => x.Remove(It.IsAny<object>()))
                    .Throws(new Exception(""));
                break;
            case "Set":
                cache.Setup(x => x.CreateEntry(It.IsAny<object>()))
                    .Throws(new Exception(""));
                break;
        }

        return cache;
    }
}
