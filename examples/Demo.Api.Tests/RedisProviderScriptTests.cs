using CachedQueries;
using CachedQueries.Redis;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Demo.Api.Tests;

/// <summary>
///     Runs RedisCacheProvider's Lua scripts against a real Redis server.
///     Each test uses its own key prefix, so it does not interfere with the API tests.
/// </summary>
[Collection("Integration")]
public class RedisProviderScriptTests(DemoApiFactory factory) : IAsyncLifetime
{
    private readonly string _prefix = $"script-test-{Guid.NewGuid():N}:";
    private ConnectionMultiplexer _redis = null!;

    public async Task InitializeAsync()
    {
        _redis = await ConnectionMultiplexer.ConnectAsync(factory.RedisConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _redis.CloseAsync();
        _redis.Dispose();
    }

    private RedisCacheProvider CreateProvider()
    {
        var unusedFallbackCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new RedisCacheProvider(unusedFallbackCache, _redis,
            NullLogger<RedisCacheProvider>.Instance, _prefix, "cq");
    }

    [Fact]
    public async Task InvalidateByTags_WithMoreMembersThanLuaUnpackLimit_ShouldDeleteAll()
    {
        // Regression: DEL unpack(members) failed with "too many results to unpack" above ~8000 members,
        // and the error was swallowed — nothing was invalidated.
        var provider = CreateProvider();
        var options = new CachingOptions { Tags = ["tag:Big"] };
        const int count = 9000;

        var batch = _redis.GetDatabase().CreateBatch();
        var writes = Enumerable.Range(0, count)
            .Select(i => provider.SetAsync($"cq:k{i}", i, options))
            .ToList();
        batch.Execute();
        await Task.WhenAll(writes);

        await provider.InvalidateByTagsAsync(["tag:Big"]);

        (await provider.GetAsync<int?>("cq:k0")).Should().BeNull();
        (await provider.GetAsync<int?>($"cq:k{count - 1}")).Should().BeNull();
        (await _redis.GetDatabase().KeyExistsAsync($"{_prefix}cq:tag:Big")).Should().BeFalse();
    }

    [Fact]
    public async Task ShortLivedEntry_ShouldNotShortenTagSetOfLongLivedEntry()
    {
        // Regression: every SET overwrote the tag set TTL, so a short entry could make the set expire
        // while a long-lived entry was still cached — that entry could then never be invalidated.
        var provider = CreateProvider();
        await provider.SetAsync("cq:long", "L", new CachingOptions { Expiration = TimeSpan.FromHours(2), Tags = ["tag:T"] });
        await provider.SetAsync("cq:short", "S", new CachingOptions { Expiration = TimeSpan.FromSeconds(1), Tags = ["tag:T"] });

        var ttl = await _redis.GetDatabase().KeyTimeToLiveAsync($"{_prefix}cq:tag:T");

        ttl.Should().BeGreaterThan(TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task SlidingEntry_HitShouldRefreshTtlAndTagSets()
    {
        var provider = CreateProvider();
        var db = _redis.GetDatabase();
        await provider.SetAsync("cq:s", "value",
            new CachingOptions { Expiration = TimeSpan.FromSeconds(30), UseSlidingExpiration = true, Tags = ["tag:S"] });

        // Shorten both TTLs artificially, then hit the entry
        await db.KeyExpireAsync($"{_prefix}cq:s", TimeSpan.FromSeconds(2));
        await db.KeyExpireAsync($"{_prefix}cq:tag:S", TimeSpan.FromSeconds(2));

        (await provider.GetAsync<string>("cq:s")).Should().Be("value");
        await db.PingAsync(); // the refresh is fire-and-forget; a round trip ensures it was processed

        (await db.KeyTimeToLiveAsync($"{_prefix}cq:s")).Should().BeGreaterThan(TimeSpan.FromSeconds(20));
        (await db.KeyTimeToLiveAsync($"{_prefix}cq:tag:S")).Should().BeGreaterThan(TimeSpan.FromSeconds(30));

        await provider.InvalidateByTagsAsync(["tag:S"]);
        (await provider.GetAsync<string>("cq:s")).Should().BeNull();
    }
}
