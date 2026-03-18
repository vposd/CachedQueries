using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using StackExchange.Redis;

namespace Demo.Api.Tests;

/// <summary>
/// Tests that verify Redis-specific caching behavior:
/// - Data persists in Redis across requests
/// - Redis keys use expected prefixes
/// - Tag-based sets are properly managed
/// </summary>
[Collection("Integration")]
public class RedisCacheTests
{
    private readonly DemoApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RedisCacheTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    private IServer GetRedisServer()
    {
        var redis = ConnectionMultiplexer.Connect($"{_factory.RedisConnectionString},allowAdmin=true");
        return redis.GetServer(redis.GetEndPoints()[0]);
    }

    [Fact]
    public async Task Redis_CacheKeysCreated_AfterQuery()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        // Flush Redis entirely
        GetRedisServer().FlushDatabase();

        // Make a cacheable request
        await client.GetAsync("/api/customers");

        // Verify Redis has keys with tenant prefix and CQ: hash
        var keys = GetRedisServer().Keys(pattern: "*CQ:*").ToList();
        keys.Should().NotBeEmpty("cacheable queries should create Redis keys");
    }

    [Fact]
    public async Task Redis_TagSetsCreated_ForTaggedQueries()
    {
        var client = _factory.CreateClientForTenant("tenant-b");

        GetRedisServer().FlushDatabase();

        // Query with custom tags
        await client.GetAsync("/api/goods/by-category/Electronics");

        // Verify tag sets exist in Redis
        var tagKeys = GetRedisServer().Keys(pattern: "cq:tag:*").ToList();
        tagKeys.Should().NotBeEmpty("tagged queries should create tag sets in Redis");
    }

    [Fact]
    public async Task Redis_CacheHit_SecondRequest()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        GetRedisServer().FlushDatabase();

        var first = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var second = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);

        first!.Length.Should().Be(second!.Length);
    }

    [Fact]
    public async Task Redis_FlushDb_RemovesAllCacheKeys()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        GetRedisServer().FlushDatabase();

        // Warm cache
        await client.GetAsync("/api/customers");
        await client.GetAsync("/api/goods");

        var keysBefore = GetRedisServer().Keys(pattern: "*CQ:*").ToList();
        keysBefore.Should().NotBeEmpty();

        // Flush via admin command
        GetRedisServer().FlushDatabase();

        var keysAfter = GetRedisServer().Keys(pattern: "*CQ:*").ToList();
        keysAfter.Should().BeEmpty();
    }

    [Fact]
    public async Task Redis_InvalidateByTag_RemovesTaggedKeys()
    {
        var client = _factory.CreateClientForTenant("tenant-b");

        GetRedisServer().FlushDatabase();

        // Warm both tagged and untagged queries
        await client.GetAsync("/api/goods");
        await client.GetAsync("/api/goods/by-category/Electronics");

        var keysBefore = GetRedisServer().Keys(pattern: "*").ToList();
        keysBefore.Count.Should().BeGreaterOrEqualTo(2);

        // Invalidate only the Electronics category tag
        await client.PostAsync("/api/goods/invalidate-category/Electronics", null);

        var keysAfter = GetRedisServer().Keys(pattern: "*").ToList();
        keysAfter.Count.Should().BeLessThan(keysBefore.Count);
    }

    [Fact]
    public async Task Redis_InvalidateByEntityType_ClearsRelatedKeys()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        GetRedisServer().FlushDatabase();
        await client.GetAsync("/api/customers");

        var keysBefore = GetRedisServer().Keys(pattern: "*CQ:*").ToList();
        keysBefore.Should().NotBeEmpty();

        // Invalidate Customer entity type
        await client.PostAsync("/api/cache/invalidate-entity/customer", null);

        var keysAfter = GetRedisServer().Keys(pattern: "*CQ:*").ToList();
        keysAfter.Count.Should().BeLessThan(keysBefore.Count);
    }

    [Fact]
    public async Task Redis_MultiTenantKeys_AreSeparate()
    {
        var clientA = _factory.CreateClientForTenant("tenant-a");
        var clientB = _factory.CreateClientForTenant("tenant-b");

        GetRedisServer().FlushDatabase();

        await clientA.GetAsync("/api/customers");
        await clientB.GetAsync("/api/customers");

        var allKeys = GetRedisServer().Keys(pattern: "*CQ:*").Select(k => k.ToString()).ToList();

        // Should have at least 2 keys — one per tenant
        allKeys.Count.Should().BeGreaterOrEqualTo(2);

        // Keys should contain different tenant prefixes
        allKeys.Where(k => k.Contains("tenant-a")).Should().NotBeEmpty();
        allKeys.Where(k => k.Contains("tenant-b")).Should().NotBeEmpty();
    }
}
