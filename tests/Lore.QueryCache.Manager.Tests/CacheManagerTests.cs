using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Lore.QueryCache.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Lore.QueryCache.Manager.Tests;

public class CacheManagerTests
{
    [Fact]
    public void Should_Set_Cache()
    {
        // Given
        var memoryCacheMock = new Mock<IMemoryCache>();

        // When
        CacheManager.Cache = new MemoryCache(memoryCacheMock.Object);
        
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

    [Theory]
    [InlineData(CacheType.MemoryCache)]
    [InlineData(CacheType.DistributedCache)]
    public async Task LinkTags_Should_Link_Invalidation_Tags_To_Cache_Key(CacheType cacheType)
    {
        // Given
        ConfigureCache(cacheType);

        // When
        CacheManager.LinkTags("key_1", new List<string>() { "tag_1", "tag_2" });
        CacheManager.LinkTags("key_2", new List<string>() { "tag_1", "tag_1",});

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
    [InlineData(CacheType.MemoryCache, null)]
    [InlineData(CacheType.MemoryCache, "")]
    [InlineData(CacheType.MemoryCache, " ")]
    [InlineData(CacheType.DistributedCache, null)]
    [InlineData(CacheType.DistributedCache, "")]
    [InlineData(CacheType.DistributedCache, " ")]
    public async Task LinkTags_Should_Not_Link_Invalidation_Tags_If_Key_Is_Empty(CacheType cacheType, string key)
    {
        // Given
        ConfigureCache(cacheType);

        // When
        CacheManager.LinkTags(key, new List<string>() { "tag_1", "tag_2" });
        CacheManager.LinkTags(key, new List<string>() { "tag_1", "tag_1", });

        // Then
        var tag1Keys = await CacheManager.Cache.GetAsync<List<string>>("test_tag_1");
        var tag2Keys = await CacheManager.Cache.GetAsync<List<string>>("test_tag_2");
        var tag3Keys = await CacheManager.Cache.GetAsync<List<string>>("test_tag_3");

        tag1Keys.Should().BeNull();
        tag2Keys.Should().BeNull();
        tag3Keys.Should().BeNull();
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
        CacheManager.LinkTags(key, new List<string>() { "tag_1", "tag_2" });
        CacheManager.LinkTags(key, new List<string>() { "tag_1", });

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
        await CacheManager.LinkTagsAsync("key_1", new List<string>() { "tag_1", "tag_2" });
        await CacheManager.LinkTagsAsync("key_2", new List<string>() { "tag_1", });

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
    public async Task InvalidateCache_Should_Invalidate_Cache_By_Tags(CacheType cacheType)
    {
        // Given
        ConfigureCache(cacheType);
        await CacheManager.Cache.SetAsync("key_1", "value_1");
        await CacheManager.Cache.SetAsync("key_2", "value_2");
        await CacheManager.LinkTagsAsync("key_1", new List<string>() { "tag_1", "tag_2" });
        await CacheManager.LinkTagsAsync("key_2", new List<string>() { "tag_1", });
        
        // When
        CacheManager.InvalidateCache(new List<string>() { "tag_2" });

        // Then
        var key1Value = await CacheManager.Cache.GetAsync<string>("key_1");
        var key2Value = await CacheManager.Cache.GetAsync<string>("key_2");
        var key3Value = await CacheManager.Cache.GetAsync<string>("key_3");

        key1Value.Should().BeNull();
        key2Value.Should().Be("value_2");
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
        await CacheManager.LinkTagsAsync("key_1", new List<string>() { "tag_1", "tag_2" });
        await CacheManager.LinkTagsAsync("key_2", new List<string>() { "tag_1", });
        
        // When
        await CacheManager.InvalidateCacheAsync(new List<string>() { "tag_2" });

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
        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IMemoryCache>();
        
        CacheManager.Cache = new MemoryCache(cache);
        CacheManager.CachePrefix = "test_";
    }
    
    private static void ConfigureDistributedCache()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        
        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();
        
        CacheManager.Cache = new DistributedCache(cache);
        CacheManager.CachePrefix = "test_";
    }

    public enum CacheType
    {
        MemoryCache,
        DistributedCache
    }
}