using CachedQueries.Core;
using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Cache;
using CachedQueries.Core.Models;
using CachedQueries.DependencyInjection;
using CachedQueries.EntityFramework;
using CachedQueries.EntityFramework.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CachedQueries.Test.DependencyInjection;

public class DependencyInjectionTest
{
    [Fact]
    public void AddCache_Should_Configure_Cache()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddCachedQueries(options => options
            .UseCacheStore<DistributedCache>()
            .UseEntityFramework()
        );

        // Then
        var provider = services.BuildServiceProvider();
        CacheManagerContainer.Initialize(provider);
        var cacheManager = CacheManagerContainer.Resolve();

        cacheManager.CacheInvalidator.Should().BeOfType<DefaultCacheInvalidator>();
        cacheManager.Config.Should().BeOfType<CachedQueriesConfig>();

        CacheManagerContainer.Reset();
    }

    [Fact]
    public void UseCache_Throw_Error_When_Empty_ServiceProvider()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddCachedQueries(options => options.UseCacheStore<DistributedCache>().UseEntityFramework());

        CacheManagerContainer.Initialize(null);
        var action = CacheManagerContainer.Resolve;

        // Then
        action.Should().Throw<ArgumentException>();
        CacheManagerContainer.Reset();
    }

    [Fact]
    public void UseCache_Should_Initialize()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddCachedQueries(options => options.UseCacheStore<DistributedCache>());

        var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        app.UseQueriesCaching();
        var cacheManager = CacheManagerContainer.Resolve();

        // Then
        cacheManager.CacheInvalidator.Should().BeOfType<DefaultCacheInvalidator>();
        cacheManager.Config.Should().BeOfType<CachedQueriesConfig>();
        cacheManager.CacheKeyFactory.Should().BeOfType<DefaultCacheKeyFactory>();

        var cacheStore = app.ApplicationServices.GetService<ICacheStore>();
        cacheStore.Should().BeOfType<DistributedCache>();
        CacheManagerContainer.Reset();
    }

    [Fact]
    public void AddCachedQueries_Should_Configure_EntityFramework()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddCachedQueries(options => options
            .UseCacheStore<DistributedCache>()
            .UseEntityFramework());

        // Then
        var provider = services.BuildServiceProvider();
        CacheManagerContainer.Initialize(provider);
        var cacheManager = CacheManagerContainer.Resolve();

        cacheManager.CacheInvalidator.Should().BeOfType<DefaultCacheInvalidator>();
        cacheManager.Config.Should().BeOfType<CachedQueriesConfig>();
        cacheManager.CacheKeyFactory.Should().BeOfType<QueryCacheKeyFactory>();

        var cacheStore = provider.GetService<ICacheStore>();
        cacheStore.Should().BeOfType<DistributedCache>();
        cacheStore.Should().BeOfType<DistributedCache>();
        CacheManagerContainer.Reset();
    }

    [Fact]
    public void AddCachedQueries_Should_Set_Cache_To_Cache_Manager()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddCachedQueries(options => options
            .UseCacheStore<MemoryCache>()
        );

        // Then
        var provider = services.BuildServiceProvider();
        CacheManagerContainer.Initialize(provider);
        var cacheManager = CacheManagerContainer.Resolve();

        cacheManager.CacheInvalidator.Should().BeOfType<DefaultCacheInvalidator>();
        cacheManager.Config.Should().BeOfType<CachedQueriesConfig>();
        cacheManager.CacheKeyFactory.Should().BeOfType<DefaultCacheKeyFactory>();

        var cacheStore = provider.GetService<ICacheStore>();
        cacheStore.Should().BeOfType<MemoryCache>();
        CacheManagerContainer.Reset();
    }

    [Fact]
    public void AddCachedQueries_Should_Config_Options()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddCachedQueries(options => options
            .UseCacheInvalidator<DefaultCacheInvalidator>()
            .UseCachingOptions(new CachedQueriesConfig
            {
                DefaultCacheDuration = TimeSpan.FromHours(1)
            })
            .UseCacheStore<MemoryCache>()
        );

        // Then
        var provider = services.BuildServiceProvider();
        CacheManagerContainer.Initialize(provider);
        var cacheManager = CacheManagerContainer.Resolve();

        cacheManager.CacheInvalidator.Should().BeOfType<DefaultCacheInvalidator>();
        cacheManager.Config.Should().BeOfType<CachedQueriesConfig>();
        cacheManager.CacheKeyFactory.Should().BeOfType<DefaultCacheKeyFactory>();

        cacheManager.Config.DefaultCacheDuration.Should().Be(TimeSpan.FromHours(1));

        var cacheStore = provider.GetService<ICacheStore>();
        cacheStore.Should().BeOfType<MemoryCache>();
        CacheManagerContainer.Reset();
    }

    [Fact]
    public void UseCacheOptions_Should_Throw_If_Options_Null()
    {
        // Given
        var services = new ServiceCollection();

        // When
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        var act = () => services.AddCachedQueries(options => options
            .UseCachingOptions(null!)
        );

        // Then
        act.Should().Throw<ArgumentNullException>();
    }
}
