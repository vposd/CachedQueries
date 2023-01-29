using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CachedQueries.Core.Cache;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MemoryCache = CachedQueries.Core.Cache.MemoryCache;

namespace CachedQueries.Core.Tests;

public class CacheManagerTests
{
    public enum CacheType
    {
        MemoryCache,
        DistributedCache
    }

    [Fact]
    public void Should_Set_Cache()
    {
        // Given
        var memoryCacheMock = new Mock<IMemoryCache>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();

        // When
        CacheManager.Cache = new MemoryCache(memoryCacheMock.Object, loggerFactoryMock.Object);

        // Then
        CacheManager.Cache.Should().BeOfType<MemoryCache>();
    }

    [Fact]
    public void Should_Throw_Error_When_Cache_Is_Not_Defined()
    {
        // Given
        CacheManager.Cache = null!;

        // When
        var action = () => CacheManager.Cache;

        // Then
        action.Should().Throw<ArgumentException>("Cache is not defined");
    }

    [Fact]
    public void Should_Throw_Error_When_LockManager_Is_Not_Defined()
    {
        // Given
        CacheManager.LockManager = null!;

        // When
        var action = () => CacheManager.LockManager;

        // Then
        action.Should().Throw<ArgumentException>("LockManager is not defined");
    }

    [Fact]
    public void Should_Throw_Error_When_CacheInvalidator_Is_Not_Defined()
    {
        // Given
        CacheManager.CacheInvalidator = null!;

        // When
        var action = () => CacheManager.CacheInvalidator;

        // Then
        action.Should().Throw<ArgumentException>("CacheInvalidator is not defined");
    }

    [Theory]
    [InlineData(CacheType.MemoryCache)]
    [InlineData(CacheType.DistributedCache)]
    public async Task Cache_Should_Set_And_Safe_Get_Values_From_Cache(CacheType cacheType)
    {
        // Given
        ConfigureCache(cacheType);

        // When
        await CacheManager.Cache.SetAsync("key_1", new List<string> { "tag_1", "tag_2" });
        await CacheManager.Cache.SetAsync("key_2", new List<string> { "tag_1", "tag_1" });

        // Then
        var tag1Keys = await CacheManager.Cache.GetAsync<List<string>>("key_1");
        var tag2Keys = await CacheManager.Cache.GetAsync<List<string>>("key_2");
        var tag2KeysWrongType = await CacheManager.Cache.GetAsync<string>("key_2");

        tag1Keys.Should().BeEquivalentTo(new List<string> { "tag_1", "tag_2" });
        tag2Keys.Should().BeEquivalentTo(new List<string> { "tag_1", "tag_1" });
        tag2KeysWrongType.Should().BeNull();
    }

    [Theory]
    [InlineData(CacheType.MemoryCache, true)]
    [InlineData(CacheType.MemoryCache, false)]
    [InlineData(CacheType.DistributedCache, true)]
    [InlineData(CacheType.DistributedCache, false)]
    public async Task Cache_Should_Delete_Values_From_Cache(CacheType cacheType, bool useLock)
    {
        // Given
        ConfigureCache(cacheType);

        await CacheManager.Cache.SetAsync("key_1", new List<string> { "tag_1", "tag_2" });
        await CacheManager.Cache.SetAsync("key_2", new List<string> { "tag_1", "tag_1" });

        // When
        await CacheManager.Cache.DeleteAsync("key_1", useLock);
        await CacheManager.Cache.DeleteAsync("key_2", useLock);

        // Then
        var tag1Keys = await CacheManager.Cache.GetAsync<List<string>>("key_1");
        var tag2Keys = await CacheManager.Cache.GetAsync<List<string>>("key_2");

        tag1Keys.Should().BeNull();
        tag2Keys.Should().BeNull();
    }

    [Theory]
    [InlineData(CacheType.MemoryCache, null)]
    [InlineData(CacheType.MemoryCache, "")]
    [InlineData(CacheType.MemoryCache, " ")]
    [InlineData(CacheType.DistributedCache, null)]
    [InlineData(CacheType.DistributedCache, "")]
    [InlineData(CacheType.DistributedCache, " ")]
    public async Task LinkTagsAsync_Should_Not_Link_Invalidation_Tags_If_Key_Is_Empty(CacheType cacheType, string key)
    {
        // Given
        ConfigureCache(cacheType);

        // When
        await CacheManager.CacheInvalidator.LinkTagsAsync(key, new List<string> { "tag_1", "tag_2" }, CancellationToken.None);
        await CacheManager.CacheInvalidator.LinkTagsAsync(key, new List<string> { "tag_1" }, CancellationToken.None);

        // Then
        var tag1Keys = await CacheManager.Cache.GetAsync<List<string>>("test_tag_1");
        var tag2Keys = await CacheManager.Cache.GetAsync<List<string>>("test_tag_2");
        var tag3Keys = await CacheManager.Cache.GetAsync<List<string>>("test_tag_3");

        tag1Keys.Should().BeNull();
        tag2Keys.Should().BeNull();
        tag3Keys.Should().BeNull();
    }

    [Theory]
    [InlineData(CacheType.MemoryCache)]
    [InlineData(CacheType.DistributedCache)]
    public async Task LinkTagsAsync_Should_Link_Invalidation_Tags_To_Cache_Key(CacheType cacheType)
    {
        // Given
        ConfigureCache(cacheType);

        // When
        await CacheManager.CacheInvalidator.LinkTagsAsync("key_1", new List<string> { "tag_1", "tag_2" }, CancellationToken.None);
        await CacheManager.CacheInvalidator.LinkTagsAsync("key_2", new List<string> { "tag_1" }, CancellationToken.None);

        // Then
        var tag1Keys = await CacheManager.Cache.GetAsync<List<string>>("test_tag_1");
        var tag2Keys = await CacheManager.Cache.GetAsync<List<string>>("test_tag_2");
        var tag3Keys = await CacheManager.Cache.GetAsync<List<string>>("test_tag_3");

        tag1Keys.Should().HaveCount(2);
        tag1Keys.Should().Contain("key_1");
        tag1Keys.Should().Contain("key_2");

        tag2Keys.Should().HaveCount(1);
        tag2Keys.Should().Contain("key_1");

        tag3Keys.Should().BeNull();
    }

    [Theory]
    [InlineData(CacheType.MemoryCache)]
    [InlineData(CacheType.DistributedCache)]
    public async Task InvalidateCacheAsync_Should_Invalidate_Cache_By_Tags(CacheType cacheType)
    {
        // Given
        ConfigureCache(cacheType);
        await CacheManager.Cache.SetAsync("key_1", "value_1");
        await CacheManager.Cache.SetAsync("key_2", "value_2");
        await CacheManager.CacheInvalidator.LinkTagsAsync("key_1", new List<string> { "tag_1", "tag_2" }, CancellationToken.None);
        await CacheManager.CacheInvalidator.LinkTagsAsync("key_2", new List<string> { "tag_1" }, CancellationToken.None);

        // When
        await CacheManager.CacheInvalidator.InvalidateCacheAsync(new List<string> { "tag_2" }, CancellationToken.None);
        await CacheManager.CacheInvalidator.InvalidateCacheAsync(new List<string> { "tag_3" }, CancellationToken.None);
        CacheManager.Cache.Log(LogLevel.Information, "All good");

        // Then
        var key1Value = await CacheManager.Cache.GetAsync<string>("key_1");
        var key2Value = await CacheManager.Cache.GetAsync<string>("key_2");

        key1Value.Should().BeNull();
        key2Value.Should().Be("value_2");
    }

    private static void ConfigureCache(CacheType cacheType)
    {
        switch (cacheType)
        {
            case CacheType.MemoryCache:
                ConfigureMemoryCache();
                break;
            case CacheType.DistributedCache:
                ConfigureDistributedCache();
                break;
        }
    }

    private static void ConfigureMemoryCache()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IMemoryCache>();
        var logger = provider.GetRequiredService<ILoggerFactory>();

        CacheManager.Cache = new MemoryCache(cache, logger);
        CacheManager.CacheInvalidator = new DefaultCacheInvalidator(CacheManager.Cache);
        CacheManager.CachePrefix = "test_";
    }

    private static void ConfigureDistributedCache()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

        CacheManager.Cache = new DistributedCache(cache, loggerFactory);
        CacheManager.CacheInvalidator = new DefaultCacheInvalidator(CacheManager.Cache);
        CacheManager.CachePrefix = "test_";
    }
}
