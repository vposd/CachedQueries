using System.Net;
using CachedQueries.Redis;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace CachedQueries.Tests;

public class RedisSlidingEntryFormatTests
{
    [Fact]
    public void BuildAndParse_ShouldRoundTrip()
    {
        var data = RedisCacheProvider.BuildSlidingEntry(60000, ["cq:tag:a", "cq:t1:tag:b"], "{\"id\":1}");

        RedisCacheProvider.TryParseSlidingEntry(data, out var windowMs, out var tagKeys, out var json)
            .Should().BeTrue();
        windowMs.Should().Be(60000);
        tagKeys.Should().Equal("cq:tag:a", "cq:t1:tag:b");
        json.Should().Be("{\"id\":1}");
    }

    [Fact]
    public void BuildAndParse_WithoutTags_ShouldRoundTrip()
    {
        var data = RedisCacheProvider.BuildSlidingEntry(5, [], "42");

        RedisCacheProvider.TryParseSlidingEntry(data, out var windowMs, out var tagKeys, out var json)
            .Should().BeTrue();
        windowMs.Should().Be(5);
        tagKeys.Should().BeEmpty();
        json.Should().Be("42");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"id\":1}")]
    [InlineData("\u0001123")] // no end of window
    [InlineData("\u0001123\u0001")] // no json separator
    [InlineData("\u0001abc\u0001\u0001{}")] // window is not a number
    public void Parse_NonSlidingOrMalformed_ShouldReturnFalseAndKeepData(string data)
    {
        RedisCacheProvider.TryParseSlidingEntry(data, out _, out _, out var json).Should().BeFalse();
        json.Should().Be(data);
    }
}

public class RedisSlidingBehaviorTests
{
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly RedisCacheProvider _provider;
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();

    public RedisSlidingBehaviorTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        _provider = new RedisCacheProvider(Substitute.For<IDistributedCache>(), _redis,
            NullLogger<RedisCacheProvider>.Instance, "", "cq");
    }

    [Fact]
    public async Task SetAsync_SlidingWithoutTags_ShouldStoreSlidingHeader()
    {
        RedisValue stored = default;
        _database.StringSetAsync(Arg.Any<RedisKey>(), Arg.Do<RedisValue>(v => stored = v), Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>()).Returns(true);

        await _provider.SetAsync("k", "v", new CachingOptions(TimeSpan.FromMinutes(1), true));

        RedisCacheProvider.TryParseSlidingEntry(stored!, out var windowMs, out _, out var json).Should().BeTrue();
        windowMs.Should().Be(60000);
        json.Should().Be("\"v\"");
    }

    [Fact]
    public async Task SetAsync_SlidingWithTags_ShouldEmbedTagKeysAndUseScript()
    {
        await _provider.SetAsync("k", "v",
            new CachingOptions { Expiration = TimeSpan.FromMinutes(1), UseSlidingExpiration = true, Tags = ["tag:x"] });

        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Is<string>(s => s.Contains("SADD")),
            Arg.Any<RedisKey[]>(),
            Arg.Is<RedisValue[]>(v => v[0].ToString().Contains("cq:tag:x") && v[2] == 60000 && v[4] == "cq:tag:x"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task GetAsync_SlidingEntry_ShouldReturnPayloadAndRefreshTtl()
    {
        var stored = RedisCacheProvider.BuildSlidingEntry(60000, ["cq:tag:x"], "\"v\"");
        _database.StringGetAsync((RedisKey)"k", Arg.Any<CommandFlags>()).Returns((RedisValue)stored);

        var result = await _provider.GetAsync<string>("k");

        result.Should().Be("v");
        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Is<string>(s => s.Contains("PEXPIRE")),
            Arg.Is<RedisKey[]>(k => k.Length == 2 && k[0] == "k" && k[1] == "cq:tag:x"),
            Arg.Is<RedisValue[]>(v => v[0] == 60000 && v[1] == 60000 + 300000),
            CommandFlags.FireAndForget);
    }

    [Fact]
    public async Task GetAsync_AbsoluteEntry_ShouldNotRefresh()
    {
        _database.StringGetAsync((RedisKey)"k", Arg.Any<CommandFlags>()).Returns((RedisValue)"\"v\"");

        (await _provider.GetAsync<string>("k")).Should().Be("v");

        await _database.DidNotReceive().ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(),
            Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ClearAsync_ShouldSkipReplicas()
    {
        var primaryEndpoint = new DnsEndPoint("primary", 6379);
        var replicaEndpoint = new DnsEndPoint("replica", 6379);
        var primary = Substitute.For<IServer>();
        var replica = Substitute.For<IServer>();
        replica.IsReplica.Returns(true);
        primary.Keys(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(),
            Arg.Any<CommandFlags>()).Returns(new RedisKey[] { "cq:a" });
        _redis.GetEndPoints(Arg.Any<bool>()).Returns([primaryEndpoint, replicaEndpoint]);
        _redis.GetServer(primaryEndpoint, Arg.Any<object>()).Returns(primary);
        _redis.GetServer(replicaEndpoint, Arg.Any<object>()).Returns(replica);

        await _provider.ClearAsync();

        replica.DidNotReceiveWithAnyArgs().Keys();
        await _database.Received(1).KeyDeleteAsync(Arg.Is<RedisKey[]>(k => k.Length == 1), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Scripts_ShouldChunkDeletesAndOnlyExtendTagTtl()
    {
        // The Lua itself runs inside Redis; guard the two properties that fixed real bugs.
        var scripts = new List<string>();
        _database.ScriptEvaluateAsync(Arg.Do<string>(scripts.Add), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>()).Returns(RedisResult.Create(0));

        await _provider.SetAsync("k", "v", new CachingOptions { Tags = ["t"] });
        await _provider.InvalidateByTagsAsync(["t"]);

        scripts[0].Should().Contain("redis.call('PTTL', ARGV[i]) < tagExpMs");
        scripts[1].Should().Contain("unpack(members, j, math.min(j + 999, #members))");
    }
}
