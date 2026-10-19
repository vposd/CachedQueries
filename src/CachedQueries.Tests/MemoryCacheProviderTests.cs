using CachedQueries.Providers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

public class MemoryCacheProviderTests : IDisposable
{
    private readonly ILogger<MemoryCacheProvider> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheProvider _provider;

    public MemoryCacheProviderTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<MemoryCacheProvider>>();
        _provider = new MemoryCacheProvider(_memoryCache, _logger);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ShouldReturnDefault()
    {
        // Act
        var result = await _provider.GetAsync<string>("non-existent-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ShouldReturnValue()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var options = new CachingOptions(TimeSpan.FromMinutes(5));

        // Act
        await _provider.SetAsync(key, value, options);
        var result = await _provider.GetAsync<string>(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public async Task SetAsync_WithSlidingExpiration_ShouldWork()
    {
        // Arrange
        var key = "sliding-key";
        var value = 42;
        var options = new CachingOptions(TimeSpan.FromMinutes(5), true);

        // Act
        await _provider.SetAsync(key, value, options);
        var result = await _provider.GetAsync<int?>(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveValue()
    {
        // Arrange
        var key = "remove-key";
        await _provider.SetAsync(key, "value", new CachingOptions());

        // Act
        await _provider.RemoveAsync(key);
        var result = await _provider.GetAsync<string>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateByTagsAsync_ShouldRemoveTaggedEntries()
    {
        // Arrange
        var key1 = "tagged-key-1";
        var key2 = "tagged-key-2";
        var key3 = "untagged-key";

        var optionsWithTag = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            Tags = ["orders"]
        };

        await _provider.SetAsync(key1, "value1", optionsWithTag);
        await _provider.SetAsync(key2, "value2", optionsWithTag);
        await _provider.SetAsync(key3, "value3", new CachingOptions());

        // Act
        await _provider.InvalidateByTagsAsync(["orders"]);

        // Assert
        (await _provider.GetAsync<string>(key1)).Should().BeNull();
        (await _provider.GetAsync<string>(key2)).Should().BeNull();
        (await _provider.GetAsync<string>(key3)).Should().Be("value3");
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllTrackedEntries()
    {
        // Arrange
        await _provider.SetAsync("key1", "value1", new CachingOptions());
        await _provider.SetAsync("key2", "value2", new CachingOptions());

        // Act
        await _provider.ClearAsync();

        // Assert
        (await _provider.GetAsync<string>("key1")).Should().BeNull();
        (await _provider.GetAsync<string>("key2")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithCancellation_ShouldThrow()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _provider.GetAsync<string>("key", cts.Token));
    }

    [Fact]
    public async Task SetAsync_WithCancellation_ShouldThrow()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _provider.SetAsync("key", "value", new CachingOptions(), cts.Token));
    }

    [Fact]
    public async Task RemoveAsync_WithCancellation_ShouldThrow()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _provider.RemoveAsync("key", cts.Token));
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithCancellation_ShouldThrow()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _provider.InvalidateByTagsAsync(["tag"], cts.Token));
    }

    [Fact]
    public async Task ClearAsync_WithCancellation_ShouldThrow()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _provider.ClearAsync(cts.Token));
    }

    [Fact]
    public async Task GetAsync_WithComplexObject_ShouldWork()
    {
        // Arrange
        var key = "complex-key";
        var value = new Order { Id = 1, Name = "Test Order", Total = 100.50m };

        await _provider.SetAsync(key, value, new CachingOptions());

        // Act
        var result = await _provider.GetAsync<Order>(key);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Test Order");
        result.Total.Should().Be(100.50m);
    }

    [Fact]
    public async Task GetAsync_WithList_ShouldWork()
    {
        // Arrange
        var key = "list-key";
        var value = new List<int> { 1, 2, 3, 4, 5 };

        await _provider.SetAsync(key, value, new CachingOptions());

        // Act
        var result = await _provider.GetAsync<List<int>>(key);

        // Assert
        result.Should().BeEquivalentTo(value);
    }

    [Fact]
    public async Task ConcurrentSetAndInvalidate_ShouldNotThrow()
    {
        // Arrange: 10 concurrent writers + 2 concurrent invalidators
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            Tags = ["orders", "reports"]
        };

        var writeTasks = Enumerable.Range(0, 10).Select(i =>
            Task.Run(async () =>
            {
                for (var j = 0; j < 50; j++)
                {
                    await _provider.SetAsync($"key-{i}-{j}", $"value-{i}-{j}", options);
                    await _provider.GetAsync<string>($"key-{i}-{j}");
                }
            }));

        var invalidateTasks = Enumerable.Range(0, 2).Select(_ =>
            Task.Run(async () =>
            {
                for (var j = 0; j < 20; j++)
                {
                    await _provider.InvalidateByTagsAsync(["orders"]);
                    await Task.Yield();
                }
            }));

        // Act & Assert: should not throw
        await Task.WhenAll(writeTasks.Concat(invalidateTasks));
    }

    [Fact]
    public async Task SetAsync_OverwritingSameKey_ShouldKeepTagTracking()
    {
        // Regression: the replaced entry's eviction callback used to wipe tracking of the new entry.
        var options = new CachingOptions { Tags = ["tag:Order"] };
        await _provider.SetAsync("k", "v1", options);
        await _provider.SetAsync("k", "v2", options);
        await Task.Delay(100); // let any background eviction callbacks run

        await _provider.InvalidateByTagsAsync(["tag:Order"]);

        (await _provider.GetAsync<string>("k")).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_AfterClear_ShouldStillBeInvalidatableByTag()
    {
        await _provider.SetAsync("before", "v", new CachingOptions { Tags = ["t"] });
        await _provider.ClearAsync();
        await _provider.SetAsync("after", "v", new CachingOptions { Tags = ["t"] });

        (await _provider.GetAsync<string>("before")).Should().BeNull();
        (await _provider.GetAsync<string>("after")).Should().Be("v");

        await _provider.InvalidateByTagsAsync(["t"]);
        (await _provider.GetAsync<string>("after")).Should().BeNull();
    }

    [Fact]
    public async Task InvalidateByTagsAsync_ShouldNotAffectEntriesWithOtherTags()
    {
        await _provider.SetAsync("a", "v", new CachingOptions { Tags = ["t1"] });
        await _provider.SetAsync("b", "v", new CachingOptions { Tags = ["t2"] });

        await _provider.InvalidateByTagsAsync(["t1", "unknown"]);

        (await _provider.GetAsync<string>("a")).Should().BeNull();
        (await _provider.GetAsync<string>("b")).Should().Be("v");
    }

    [Fact]
    public async Task ConcurrentOverwriteAndInvalidate_ShouldNeverLeaveSurvivingEntries()
    {
        var options = new CachingOptions { Tags = ["hot"] };

        var writers = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            for (var j = 0; j < 200; j++)
            {
                await _provider.SetAsync($"k{j % 5}", $"v{i}-{j}", options);
            }
        }));
        await Task.WhenAll(writers);

        await _provider.InvalidateByTagsAsync(["hot"]);

        for (var j = 0; j < 5; j++)
        {
            (await _provider.GetAsync<string>($"k{j}")).Should().BeNull();
        }
    }

    [Fact]
    public async Task ConcurrentSetRemoveAndClear_ShouldNotThrow()
    {
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            Tags = ["tag1"]
        };

        var tasks = Enumerable.Range(0, 10).Select(i =>
            Task.Run(async () =>
            {
                for (var j = 0; j < 50; j++)
                {
                    var key = $"concurrent-{i}-{j}";
                    await _provider.SetAsync(key, "value", options);
                    await _provider.RemoveAsync(key);
                }
            }));

        var clearTask = Task.Run(async () =>
        {
            for (var j = 0; j < 5; j++)
            {
                await _provider.ClearAsync();
                await Task.Yield();
            }
        });

        // Act & Assert: should not throw
        await Task.WhenAll(tasks.Append(clearTask));
    }
}
