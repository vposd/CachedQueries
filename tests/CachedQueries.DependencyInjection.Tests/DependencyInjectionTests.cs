using System.Collections.Generic;
using CachedQueries.Core;
using CachedQueries.Core.Cache;
using CachedQueries.Core.Enums;
using CachedQueries.Core.Interfaces;
using CachedQueries.EntityFramework;
using FluentAssertions;
using Microsoft.AspNetCore.Builder.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CachedQueries.DependencyInjection.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddCache_Should_Configure_Cache()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddQueriesCaching(options => options
            .UseCacheStore<DistributedCache>()
        );

        // Then
        var provider = services.BuildServiceProvider();
        var cacheManager = provider.GetRequiredService<ICacheManager>();

        cacheManager.CacheInvalidator.Should().BeOfType<DefaultCacheInvalidator>();
        cacheManager.CacheOptions.Should().BeOfType<CacheOptions>();
        cacheManager.CacheKeyFactory.Should().BeOfType<CacheKeyFactory>();
        cacheManager.LockManager.Should().BeOfType<DefaultLockManager>();
        cacheManager.CacheStoreProvider.Should().BeOfType<CacheStoreProvider>();

        var cacheStore =
            cacheManager.CacheStoreProvider.GetCacheStore(string.Empty, new List<string>(), CacheContentType.Object);
        cacheStore.Should().BeOfType<DistributedCache>();
    }

    [Fact]
    public void AddLoreCache_Should_Configure_EntityFramework()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddQueriesCaching(options => options.UseCacheStore<DistributedCache>().UseEntityFramework());

        // Then
        var provider = services.BuildServiceProvider();
        var cacheManager = provider.GetRequiredService<ICacheManager>();

        cacheManager.CacheInvalidator.Should().BeOfType<DefaultCacheInvalidator>();
        cacheManager.CacheOptions.Should().BeOfType<CacheOptions>();
        cacheManager.CacheKeyFactory.Should().BeOfType<QueryCacheKeyFactory>();
        cacheManager.LockManager.Should().BeOfType<DefaultLockManager>();
        cacheManager.CacheStoreProvider.Should().BeOfType<CacheStoreProvider>();

        var cacheStore =
            cacheManager.CacheStoreProvider.GetCacheStore(string.Empty, new List<string>(), CacheContentType.Object);
        cacheStore.Should().BeOfType<DistributedCache>();
    }

    [Fact]
    public void UseLoreCache_Should_Set_Cache_To_Cache_Manager()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddQueriesCaching(options => options
            .UseCacheStore<MemoryCache>()
        );

        // Then
        var provider = services.BuildServiceProvider();
        var cacheManager = provider.GetRequiredService<ICacheManager>();

        cacheManager.CacheInvalidator.Should().BeOfType<DefaultCacheInvalidator>();
        cacheManager.CacheOptions.Should().BeOfType<CacheOptions>();
        cacheManager.CacheKeyFactory.Should().BeOfType<CacheKeyFactory>();
        cacheManager.LockManager.Should().BeOfType<DefaultLockManager>();
        cacheManager.CacheStoreProvider.Should().BeOfType<CacheStoreProvider>();

        var cacheStore =
            cacheManager.CacheStoreProvider.GetCacheStore(string.Empty, new List<string>(), CacheContentType.Object);
        cacheStore.Should().BeOfType<MemoryCache>();
    }
}
