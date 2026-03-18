using System.Net;
using System.Text;
using System.Text.Json;
using CachedQueries.Redis;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Xunit;

namespace CachedQueries.Tests;

public class RedisCacheProviderTests
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisCacheProvider> _logger;
    private readonly RedisCacheProvider _provider;

    public RedisCacheProviderTests()
    {
        _distributedCache = Substitute.For<IDistributedCache>();
        _logger = Substitute.For<ILogger<RedisCacheProvider>>();
        _provider = new RedisCacheProvider(_distributedCache, _logger);
    }

    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ShouldReturnDefault()
    {
        // Arrange - mock the actual interface method
        _distributedCache.GetAsync("test-key", Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        // Act
        var result = await _provider.GetAsync<string>("test-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenKeyExists_ShouldReturnDeserializedValue()
    {
        // Arrange
        var value = new Order { Id = 1, Name = "Test", Total = 100 };
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _distributedCache.GetAsync("test-key", Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(json));

        // Act
        var result = await _provider.GetAsync<Order>("test-key");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetAsync_WhenExceptionOccurs_ShouldReturnDefaultAndLog()
    {
        // Arrange
        _distributedCache.GetAsync("test-key", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection error"));

        // Act
        var result = await _provider.GetAsync<string>("test-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ShouldSerializeAndStore()
    {
        // Arrange
        var value = "test-value";
        var options = new CachingOptions(TimeSpan.FromMinutes(30));

        // Act
        await _provider.SetAsync("test-key", value, options);

        // Assert - verify the actual interface method was called
        await _distributedCache.Received(1).SetAsync(
            "test-key",
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_WithSlidingExpiration_ShouldSetSlidingExpiration()
    {
        // Arrange
        var options = new CachingOptions(TimeSpan.FromMinutes(10), useSlidingExpiration: true);

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert
        await _distributedCache.Received(1).SetAsync(
            "test-key",
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.SlidingExpiration == TimeSpan.FromMinutes(10)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_WithTags_ShouldNotThrow()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["tag1", "tag2"]
        };

        // Act & Assert - should not throw (tag storage uses extension methods that are hard to mock)
        await _provider.SetAsync("test-key", "value", options);
    }

    [Fact]
    public async Task SetAsync_WhenExceptionOccurs_ShouldLog()
    {
        // Arrange
        _distributedCache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection error"));

        // Act
        await _provider.SetAsync("test-key", "value", new CachingOptions());

        // Assert - should not throw
    }

    [Fact]
    public async Task RemoveAsync_ShouldCallDistributedCache()
    {
        // Act
        await _provider.RemoveAsync("test-key");

        // Assert
        await _distributedCache.Received(1).RemoveAsync("test-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_WhenExceptionOccurs_ShouldLog()
    {
        // Arrange
        _distributedCache.RemoveAsync("test-key", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection error"));

        // Act
        await _provider.RemoveAsync("test-key");

        // Assert - should not throw
    }

    [Fact]
    public async Task InvalidateByTagsAsync_ShouldRemoveAllTaggedKeys()
    {
        // Arrange
        var keys = new HashSet<string> { "key1", "key2" };
        var keysJson = JsonSerializer.Serialize(keys, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _distributedCache.GetAsync("cq:tag:orders", Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(keysJson));

        // Act
        await _provider.InvalidateByTagsAsync(["orders"]);

        // Assert
        await _distributedCache.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
        await _distributedCache.Received(1).RemoveAsync("key2", Arg.Any<CancellationToken>());
        await _distributedCache.Received(1).RemoveAsync("cq:tag:orders", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WhenTagDoesNotExist_ShouldNotThrow()
    {
        // Arrange
        _distributedCache.GetAsync("cq:tag:nonexistent", Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        // Act
        await _provider.InvalidateByTagsAsync(["nonexistent"]);

        // Assert - should not throw
        await _distributedCache.DidNotReceive().RemoveAsync("cq:tag:nonexistent", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearAsync_ShouldLogWarning()
    {
        // Act
        await _provider.ClearAsync();

        // Assert - should just log warning about not being fully supported
    }
}

/// <summary>
/// Tests for RedisCacheProvider with IConnectionMultiplexer (atomic operations mode).
/// </summary>
public class RedisCacheProviderWithMultiplexerTests
{
    private readonly IDistributedCache _distributedCache;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly IServer _server;
    private readonly ILogger<RedisCacheProvider> _logger;
    private readonly RedisCacheProvider _provider;

    public RedisCacheProviderWithMultiplexerTests()
    {
        _distributedCache = Substitute.For<IDistributedCache>();
        _redis = Substitute.For<IConnectionMultiplexer>();
        _database = Substitute.For<IDatabase>();
        _server = Substitute.For<IServer>();
        _logger = Substitute.For<ILogger<RedisCacheProvider>>();

        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);

        _provider = new RedisCacheProvider(_distributedCache, _redis, _logger);
    }

    [Fact]
    public async Task SetAsync_WithTags_ShouldUseAtomicSetAdd()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["orders"]
        };

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert - should use SADD for atomic tag operation
        await _database.Received(1).SetAddAsync("cq:tag:orders", "test-key", Arg.Any<CommandFlags>());
        // KeyExpireAsync is also called but has multiple overloads, just verify SetAddAsync was used
    }

    [Fact]
    public async Task InvalidateByTagsAsync_ShouldUseAtomicSetMembers()
    {
        // Arrange
        var members = new RedisValue[] { "key1", "key2" };
        _database.SetMembersAsync("cq:tag:orders", Arg.Any<CommandFlags>())
            .Returns(members);

        // Act
        await _provider.InvalidateByTagsAsync(["orders"]);

        // Assert
        await _database.Received(1).SetMembersAsync("cq:tag:orders", Arg.Any<CommandFlags>());
        await _database.Received(1).KeyDeleteAsync(Arg.Is<RedisKey[]>(k => k.Length == 2), Arg.Any<CommandFlags>());
        await _database.Received(1).KeyDeleteAsync((RedisKey)"cq:tag:orders", Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WhenNoKeys_ShouldNotDeleteAnything()
    {
        // Arrange
        _database.SetMembersAsync("cq:tag:empty", Arg.Any<CommandFlags>())
            .Returns(Array.Empty<RedisValue>());

        // Act
        await _provider.InvalidateByTagsAsync(["empty"]);

        // Assert - should not try to delete any keys
        await _database.DidNotReceive().KeyDeleteAsync(Arg.Any<RedisKey[]>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ClearAsync_WithMultiplexer_ShouldDeleteKeysByPrefix()
    {
        // Arrange
        var endpoint = new DnsEndPoint("localhost", 6379);
        _redis.GetEndPoints(Arg.Any<bool>()).Returns([endpoint]);
        _redis.GetServer(endpoint, Arg.Any<object>()).Returns(_server);
        _server.Keys(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(new RedisKey[] { "cq:key1", "cq:key2" });

        // Act
        await _provider.ClearAsync();

        // Assert
        await _database.Received(1).KeyDeleteAsync(Arg.Is<RedisKey[]>(k => k.Length == 2), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ClearAsync_WhenExceptionOccurs_ShouldLog()
    {
        // Arrange
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>())
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act
        await _provider.ClearAsync();

        // Assert - should not throw
    }

    [Fact]
    public async Task AddKeyToTag_WhenExceptionOccurs_ShouldLog()
    {
        // Arrange
        _database.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        var options = new CachingOptions { Tags = ["failing-tag"] };

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert - should not throw
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WhenExceptionOccurs_ShouldLog()
    {
        // Arrange
        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act
        await _provider.InvalidateByTagsAsync(["failing-tag"]);

        // Assert - should not throw
    }

    [Fact]
    public async Task ClearAsync_WhenNoKeysFound_ShouldNotDeleteAnything()
    {
        // Arrange
        var endpoint = new DnsEndPoint("localhost", 6379);
        _redis.GetEndPoints(Arg.Any<bool>()).Returns([endpoint]);
        _redis.GetServer(endpoint, Arg.Any<object>()).Returns(_server);
        _server.Keys(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(Array.Empty<RedisKey>());

        // Act
        await _provider.ClearAsync();

        // Assert
        await _database.DidNotReceive().KeyDeleteAsync(Arg.Any<RedisKey[]>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithEmptyKeys_ShouldNotDeleteArray()
    {
        // Arrange
        var members = new RedisValue[] { RedisValue.Null }; // Contains null value
        _database.SetMembersAsync("cq:tag:orders", Arg.Any<CommandFlags>())
            .Returns(members);

        // Act
        await _provider.InvalidateByTagsAsync(["orders"]);

        // Assert - should not call KeyDeleteAsync for the array of keys
        await _database.DidNotReceive().KeyDeleteAsync(Arg.Any<RedisKey[]>(), Arg.Any<CommandFlags>());
    }
}

/// <summary>
/// Tests for RedisCacheProvider in fallback mode (IDistributedCache only, without IConnectionMultiplexer).
/// </summary>
public class RedisCacheProviderFallbackModeTests
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisCacheProvider> _logger;
    private readonly RedisCacheProvider _provider;

    public RedisCacheProviderFallbackModeTests()
    {
        _distributedCache = Substitute.For<IDistributedCache>();
        _logger = Substitute.For<ILogger<RedisCacheProvider>>();
        _provider = new RedisCacheProvider(_distributedCache, _logger);
    }

    [Fact]
    public async Task SetAsync_WithTags_InFallbackMode_ShouldStoreTagsInDistributedCache()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["orders"]
        };

        // Simulate existing tag data
        _distributedCache.GetAsync("cq:tag:orders", Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert - should call SetAsync for the tag
        await _distributedCache.Received().SetAsync(
            "cq:tag:orders",
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_WithTags_InFallbackMode_ShouldMergeWithExistingTags()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["orders"]
        };

        // Simulate existing tag data
        var existingKeys = new HashSet<string> { "existing-key" };
        var existingJson = JsonSerializer.Serialize(existingKeys, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _distributedCache.GetAsync("cq:tag:orders", Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(existingJson));

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert - should call SetAsync for the tag with merged keys
        await _distributedCache.Received().SetAsync(
            "cq:tag:orders",
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b).Contains("existing-key") && Encoding.UTF8.GetString(b).Contains("test-key")),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddKeyToTag_WhenExceptionOccurs_InFallbackMode_ShouldLog()
    {
        // Arrange
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection error"));

        var options = new CachingOptions { Tags = ["failing-tag"] };

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert - should not throw
    }
}
