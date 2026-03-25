using CachedQueries.Abstractions;
using CachedQueries.Redis;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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
        services.AddCachedQueriesWithRedis(config => { config.AutoInvalidation = false; });
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
        var mockMultiplexer = Substitute.For<IConnectionMultiplexer>();
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

    [Fact]
    public async Task AddCachedQueriesWithRedis_WithInstanceName_ShouldUseSamePrefixForAllPaths()
    {
        // Arrange — mimics real app: AddStackExchangeRedisCache with InstanceName + IConnectionMultiplexer
        var services = new ServiceCollection();
        services.AddLogging();

        var mockDatabase = Substitute.For<IDatabase>();
        var mockMultiplexer = Substitute.For<IConnectionMultiplexer>();
        mockMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(mockDatabase);
        services.AddSingleton(mockMultiplexer);

        // Register Redis cache with InstanceName — this is what IDistributedCache uses as prefix
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = "localhost:6379";
            options.InstanceName = "CacheDevelopment:";
        });

        // Act — no explicit KeyPrefix, should auto-detect InstanceName
        services.AddCachedQueriesWithRedis();
        var sp = services.BuildServiceProvider();

        var provider = sp.GetRequiredService<RedisCacheProvider>();

        // Capture the key used by IDatabase.StringSetAsync
        string? capturedKey = null;
        mockDatabase.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                capturedKey = ci.ArgAt<RedisKey>(0);
                return true;
            });

        await provider.SetAsync("cq:abc123", "hello", new CachingOptions(TimeSpan.FromMinutes(5)));

        // Assert — the IDatabase key must include the InstanceName prefix
        capturedKey.Should().Be("CacheDevelopment:cq:abc123",
            "IDatabase operations must use the same prefix as IDistributedCache (InstanceName)");
    }

    [Fact]
    public async Task AddCachedQueriesWithRedis_WithoutInstanceName_ShouldNotPrefix()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var mockDatabase = Substitute.For<IDatabase>();
        var mockMultiplexer = Substitute.For<IConnectionMultiplexer>();
        mockMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(mockDatabase);
        services.AddSingleton(mockMultiplexer);

        // No InstanceName set
        services.AddDistributedMemoryCache();

        services.AddCachedQueriesWithRedis();
        var sp = services.BuildServiceProvider();

        var provider = sp.GetRequiredService<RedisCacheProvider>();

        string? capturedKey = null;
        mockDatabase.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                capturedKey = ci.ArgAt<RedisKey>(0);
                return true;
            });

        await provider.SetAsync("cq:abc123", "hello", new CachingOptions(TimeSpan.FromMinutes(5)));

        // Assert — no prefix, just the raw key
        capturedKey.Should().Be("cq:abc123");
    }
}
