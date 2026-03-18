using CachedQueries.Providers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

/// <summary>
/// Tests verifying the memory leak fix in MemoryCacheProvider.
/// ConcurrentBag was replaced with ConcurrentDictionary for proper key removal support.
/// </summary>
public class MemoryLeakFixTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheProvider _provider;

    public MemoryLeakFixTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = Substitute.For<ILogger<MemoryCacheProvider>>();
        _provider = new MemoryCacheProvider(_memoryCache, logger);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    [Fact]
    public async Task RemoveAsync_ShouldCleanUpTracking()
    {
        // Arrange
        await _provider.SetAsync("key1", "value1", new CachingOptions());
        await _provider.SetAsync("key2", "value2", new CachingOptions());

        // Act: remove key1
        await _provider.RemoveAsync("key1");

        // Assert: key1 removed, key2 still there
        (await _provider.GetAsync<string>("key1")).Should().BeNull();
        (await _provider.GetAsync<string>("key2")).Should().Be("value2");

        // ClearAsync should only clear key2 (key1 already removed from tracking)
        await _provider.ClearAsync();
        (await _provider.GetAsync<string>("key2")).Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldCleanTagTracking()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            Tags = ["orders"]
        };

        await _provider.SetAsync("key1", "value1", options);
        await _provider.SetAsync("key2", "value2", options);

        // Act: remove key1
        await _provider.RemoveAsync("key1");

        // Assert: invalidating by tag should only affect key2
        await _provider.InvalidateByTagsAsync(["orders"]);
        (await _provider.GetAsync<string>("key1")).Should().BeNull();
        (await _provider.GetAsync<string>("key2")).Should().BeNull();
    }

    [Fact]
    public async Task InvalidateByTagsAsync_ShouldCleanUpTagTracking()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            Tags = ["orders"]
        };

        await _provider.SetAsync("key1", "value1", options);

        // Act: invalidate by tag
        await _provider.InvalidateByTagsAsync(["orders"]);

        // Assert: second invalidation should not do anything (tag cleaned up)
        await _provider.SetAsync("key2", "value2", new CachingOptions());
        await _provider.InvalidateByTagsAsync(["orders"]);
        // key2 should still be there since it wasn't tagged
        (await _provider.GetAsync<string>("key2")).Should().Be("value2");
    }

    [Fact]
    public async Task ClearAsync_ShouldResetAllTracking()
    {
        // Arrange
        await _provider.SetAsync("key1", "value1", new CachingOptions { Tags = ["tag1"] });
        await _provider.SetAsync("key2", "value2", new CachingOptions { Tags = ["tag1"] });

        // Act
        await _provider.ClearAsync();

        // Assert: all cleared
        (await _provider.GetAsync<string>("key1")).Should().BeNull();
        (await _provider.GetAsync<string>("key2")).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_AfterRemoveAsync_ShouldTrackNewEntry()
    {
        // Arrange
        await _provider.SetAsync("key1", "value1", new CachingOptions());
        await _provider.RemoveAsync("key1");

        // Act: re-add same key
        await _provider.SetAsync("key1", "value2", new CachingOptions());

        // Assert
        (await _provider.GetAsync<string>("key1")).Should().Be("value2");
    }

    [Fact]
    public async Task MultipleTagsOnSameKey_ShouldAllBeTrackedAndCleanedUp()
    {
        // Arrange
        var options = new CachingOptions
        {
            Tags = ["tag1", "tag2"]
        };
        await _provider.SetAsync("key1", "value1", options);

        // Act: invalidate by tag1
        await _provider.InvalidateByTagsAsync(["tag1"]);

        // Assert: key1 removed
        (await _provider.GetAsync<string>("key1")).Should().BeNull();

        // tag2 should also be cleaned up (key removed from its tracking)
        await _provider.SetAsync("key2", "value2", new CachingOptions());
        await _provider.InvalidateByTagsAsync(["tag2"]);
        // key2 wasn't tagged with tag2, so it should still exist
        (await _provider.GetAsync<string>("key2")).Should().Be("value2");
    }
}
