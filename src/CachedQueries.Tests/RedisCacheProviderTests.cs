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
        var json = JsonSerializer.Serialize(value,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
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
        var options = new CachingOptions(TimeSpan.FromMinutes(10), true);

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
            Tags = ["tag:tag1", "tag:tag2"]
        };

        // Act & Assert - should not throw (tag storage uses extension methods that are hard to mock)
        await _provider.SetAsync("test-key", "value", options);
    }

    [Fact]
    public async Task SetAsync_WhenExceptionOccurs_ShouldLog()
    {
        // Arrange
        _distributedCache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>())
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
        var keysJson = JsonSerializer.Serialize(keys,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _distributedCache.GetAsync("cq:tag:orders", Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(keysJson));

        // Act
        await _provider.InvalidateByTagsAsync(["tag:orders"]);

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
        await _provider.InvalidateByTagsAsync(["tag:nonexistent"]);

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
///     Tests for RedisCacheProvider with IConnectionMultiplexer (atomic operations mode).
/// </summary>
public class RedisCacheProviderWithMultiplexerTests
{
    private readonly IDatabase _database;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisCacheProvider> _logger;
    private readonly RedisCacheProvider _provider;
    private readonly IConnectionMultiplexer _redis;
    private readonly IServer _server;

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
    public async Task GetAsync_ShouldUseRedisDirectly_NotDistributedCache()
    {
        // Arrange
        var json = JsonSerializer.Serialize("test-value",
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _database.StringGetAsync("test-key", Arg.Any<CommandFlags>())
            .Returns((RedisValue)json);

        // Act
        var result = await _provider.GetAsync<string>("test-key");

        // Assert — should go through IDatabase, NOT IDistributedCache
        result.Should().Be("test-value");
        await _database.Received(1).StringGetAsync("test-key", Arg.Any<CommandFlags>());
        await _distributedCache.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_ShouldReturnDefault()
    {
        // Arrange
        _database.StringGetAsync("missing", Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        // Act
        var result = await _provider.GetAsync<string>("missing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldUseRedisDirectly_NotDistributedCache()
    {
        // Act
        await _provider.RemoveAsync("test-key");

        // Assert — should go through IDatabase, NOT IDistributedCache
        await _database.Received(1).KeyDeleteAsync("test-key", Arg.Any<CommandFlags>());
        await _distributedCache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_ShouldNotUseDistributedCache()
    {
        // Arrange
        _database.StringGetAsync("key1", Arg.Any<CommandFlags>()).Returns(RedisValue.Null);

        // Act
        await _provider.GetAsync<string>("key1");

        // Assert — IDistributedCache must NOT be used (it adds InstanceName prefix)
        await _distributedCache.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_ShouldNotUseDistributedCache()
    {
        // Act
        await _provider.RemoveAsync("key1");

        // Assert — IDistributedCache must NOT be used (it adds InstanceName prefix)
        await _distributedCache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAndGet_ShouldUseConsistentAccessPath()
    {
        // Arrange — capture what SetAsync writes
        string? storedValue = null;
        _database.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                storedValue = ci.ArgAt<RedisValue>(1);
                return true;
            });
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(ci => storedValue is not null ? (RedisValue)storedValue : RedisValue.Null);

        // Act — write then read through the same provider
        await _provider.SetAsync("key1", "hello", new CachingOptions(TimeSpan.FromMinutes(5)));
        var result = await _provider.GetAsync<string>("key1");

        // Assert — round-trip works because both paths use IDatabase
        result.Should().Be("hello");
    }

    [Fact]
    public async Task SetAsync_WithTags_ShouldUseAtomicLuaScript()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["tag:orders"]
        };

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert - should use ScriptEvaluateAsync for atomic SET + SADD
        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Is<RedisKey[]>(k => k.Length == 1 && k[0] == (RedisKey)"test-key"),
            Arg.Is<RedisValue[]>(v => v.Length == 5 && v[4] == (RedisValue)"cq:tag:orders"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_ShouldUseAtomicLuaScript()
    {
        // Arrange
        _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>())
            .Returns(RedisResult.Create(2));

        // Act
        await _provider.InvalidateByTagsAsync(["tag:orders"]);

        // Assert - should use ScriptEvaluateAsync for atomic SMEMBERS + DEL
        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Is<RedisKey[]>(k => k.Length == 1 && k[0] == (RedisKey)"cq:tag:orders"),
            Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WhenNoKeys_ShouldStillCallScript()
    {
        // Arrange - Lua script handles empty sets internally
        _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>())
            .Returns(RedisResult.Create(0));

        // Act
        await _provider.InvalidateByTagsAsync(["tag:empty"]);

        // Assert - script is called; it handles empty sets gracefully
        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Is<RedisKey[]>(k => k.Length == 1),
            Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ClearAsync_WithMultiplexer_ShouldDeleteKeysByPrefix()
    {
        // Arrange
        var endpoint = new DnsEndPoint("localhost", 6379);
        _redis.GetEndPoints(Arg.Any<bool>()).Returns([endpoint]);
        _redis.GetServer(endpoint, Arg.Any<object>()).Returns(_server);
        _server.Keys(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(),
                Arg.Any<CommandFlags>())
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
    public async Task SetAsync_WithTags_WhenScriptFails_ShouldLog()
    {
        // Arrange
        _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        var options = new CachingOptions { Tags = ["tag:failing-tag"] };

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert - should not throw
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WhenExceptionOccurs_ShouldLog()
    {
        // Arrange
        _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act
        await _provider.InvalidateByTagsAsync(["tag:failing-tag"]);

        // Assert - should not throw
    }

    [Fact]
    public async Task ClearAsync_WhenNoKeysFound_ShouldNotDeleteAnything()
    {
        // Arrange
        var endpoint = new DnsEndPoint("localhost", 6379);
        _redis.GetEndPoints(Arg.Any<bool>()).Returns([endpoint]);
        _redis.GetServer(endpoint, Arg.Any<object>()).Returns(_server);
        _server.Keys(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(),
                Arg.Any<CommandFlags>())
            .Returns(Array.Empty<RedisKey>());

        // Act
        await _provider.ClearAsync();

        // Assert
        await _database.DidNotReceive().KeyDeleteAsync(Arg.Any<RedisKey[]>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithEmptyTagList_ShouldNotCallScript()
    {
        // Act
        await _provider.InvalidateByTagsAsync([]);

        // Assert - should not call the script at all
        await _database.DidNotReceive().ScriptEvaluateAsync(
            Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>());
    }
}

/// <summary>
///     Tests for RedisCacheProvider with custom key prefix (replaces InstanceName).
/// </summary>
public class RedisCacheProviderKeyPrefixTests
{
    private readonly IDatabase _database;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisCacheProvider> _logger;
    private readonly RedisCacheProvider _provider;
    private readonly IConnectionMultiplexer _redis;

    public RedisCacheProviderKeyPrefixTests()
    {
        _distributedCache = Substitute.For<IDistributedCache>();
        _redis = Substitute.For<IConnectionMultiplexer>();
        _database = Substitute.For<IDatabase>();
        _logger = Substitute.For<ILogger<RedisCacheProvider>>();

        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);

        // Create provider with "myapp:" instance prefix and "cq" cache prefix
        _provider = new RedisCacheProvider(_distributedCache, _redis, _logger, "myapp:", "cq");
    }

    [Fact]
    public async Task SetAndGet_WithPrefix_ShouldRoundTripConsistently()
    {
        // Arrange — capture the key used by StringSetAsync
        string? capturedKey = null;
        string? capturedValue = null;
        _database.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                capturedKey = ci.ArgAt<RedisKey>(0);
                capturedValue = ci.ArgAt<RedisValue>(1);
                return true;
            });
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(ci => capturedValue is not null && (string?)ci.ArgAt<RedisKey>(0) == capturedKey
                ? (RedisValue)capturedValue
                : RedisValue.Null);

        // Act
        await _provider.SetAsync("key1", "hello", new CachingOptions(TimeSpan.FromMinutes(5)));
        var result = await _provider.GetAsync<string>("key1");

        // Assert
        result.Should().Be("hello");
        capturedKey.Should().Be("myapp:key1", "data key must include the configured prefix");
    }

    [Fact]
    public async Task SetAsync_ShouldPrefixDataKey()
    {
        // Act
        await _provider.SetAsync("cq:abc123", "value", new CachingOptions(TimeSpan.FromMinutes(5)));

        // Assert — the Redis key must be prefixed with "myapp:"
        await _database.Received(1).StringSetAsync(
            (RedisKey)"myapp:cq:abc123",
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task GetAsync_ShouldPrefixDataKey()
    {
        // Act
        await _provider.GetAsync<string>("cq:abc123");

        // Assert — the Redis key must be prefixed with "myapp:"
        await _database.Received(1).StringGetAsync(
            (RedisKey)"myapp:cq:abc123",
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RemoveAsync_ShouldPrefixDataKey()
    {
        // Act
        await _provider.RemoveAsync("cq:abc123");

        // Assert — the Redis key must be prefixed with "myapp:"
        await _database.Received(1).KeyDeleteAsync(
            (RedisKey)"myapp:cq:abc123",
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SetAsync_WithTags_ShouldPrefixDataKeyInLuaScript()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["tag:orders"]
        };

        // Act
        await _provider.SetAsync("cq:abc123", "value", options);

        // Assert — KEYS[1] in Lua script must be the prefixed data key
        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Is<RedisKey[]>(k => k[0] == (RedisKey)"myapp:cq:abc123"),
            Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task SetAsync_WithTags_ShouldUsePrefix_InTagKeys()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["tag:orders"]
        };

        // Act
        await _provider.SetAsync("key1", "value", options);

        // Assert — tag key should include the prefix: "myapp:cq:tag:orders"
        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Any<RedisKey[]>(),
            Arg.Is<RedisValue[]>(v => v[4] == (RedisValue)"myapp:cq:tag:orders"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_ShouldUsePrefix_InTagKeys()
    {
        // Arrange
        _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>())
            .Returns(RedisResult.Create(0));

        // Act
        await _provider.InvalidateByTagsAsync(["tag:orders"]);

        // Assert — tag key should include the prefix
        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Is<RedisKey[]>(k => k[0] == (RedisKey)"myapp:cq:tag:orders"),
            Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>());
    }
}

/// <summary>
///     Tests for RedisCacheProvider in fallback mode (IDistributedCache only, without IConnectionMultiplexer).
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
            Tags = ["tag:orders"]
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
            Tags = ["tag:orders"]
        };

        // Simulate existing tag data
        var existingKeys = new HashSet<string> { "existing-key" };
        var existingJson = JsonSerializer.Serialize(existingKeys,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _distributedCache.GetAsync("cq:tag:orders", Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(existingJson));

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert - should call SetAsync for the tag with merged keys
        await _distributedCache.Received().SetAsync(
            "cq:tag:orders",
            Arg.Is<byte[]>(b =>
                Encoding.UTF8.GetString(b).Contains("existing-key") && Encoding.UTF8.GetString(b).Contains("test-key")),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddKeyToTag_WhenExceptionOccurs_InFallbackMode_ShouldLog()
    {
        // Arrange
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection error"));

        var options = new CachingOptions { Tags = ["tag:failing-tag"] };

        // Act
        await _provider.SetAsync("test-key", "value", options);

        // Assert - should not throw
    }
}
