using CachedQueries.Abstractions;
using CachedQueries.Redis;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace CachedQueries.Tests;

public class RedisServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCachedQueriesWithRedis_WithoutConnectionMultiplexer_ShouldRegisterBasicProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();

        // Act
        services.AddCachedQueriesWithRedis();
        var provider = services.BuildServiceProvider();

        // Assert - resolve to trigger factory execution
        var cacheProvider = provider.GetService<ICacheProvider>();
        cacheProvider.Should().NotBeNull();
        cacheProvider.Should().BeOfType<RedisCacheProvider>();
        
        // Also resolve the concrete type to cover factory lines
        var concreteProvider = provider.GetService<RedisCacheProvider>();
        concreteProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddCachedQueriesWithRedis_WithConfiguration_ShouldApplyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();

        // Act
        services.AddCachedQueriesWithRedis(config =>
        {
            config.AutoInvalidation = false;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var config = provider.GetService<CachedQueriesConfiguration>();
        config.Should().NotBeNull();
        config!.AutoInvalidation.Should().BeFalse();
        
        // Resolve provider to trigger factory
        var cacheProvider = provider.GetService<RedisCacheProvider>();
        cacheProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddCachedQueriesWithRedis_WithConnectionMultiplexer_ShouldRegisterAtomicProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();

        // Mock IConnectionMultiplexer
        var mockMultiplexer = NSubstitute.Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(mockMultiplexer);

        // Act
        services.AddCachedQueriesWithRedis();
        var provider = services.BuildServiceProvider();

        // Assert - resolve to trigger factory with multiplexer branch
        var cacheProvider = provider.GetService<ICacheProvider>();
        cacheProvider.Should().NotBeNull();
        cacheProvider.Should().BeOfType<RedisCacheProvider>();
        
        // Also resolve concrete type
        var concreteProvider = provider.GetService<RedisCacheProvider>();
        concreteProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddCachedQueriesWithRedis_ShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();

        // Act
        services.AddCachedQueriesWithRedis();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<ICacheProvider>().Should().NotBeNull();
        provider.GetService<ICacheKeyGenerator>().Should().NotBeNull();
        provider.GetService<ICacheInvalidator>().Should().NotBeNull();
        provider.GetService<ICacheProviderFactory>().Should().NotBeNull();
        provider.GetService<CachedQueriesConfiguration>().Should().NotBeNull();
        provider.GetService<RedisCacheProvider>().Should().NotBeNull();
    }
}
