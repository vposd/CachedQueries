using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Demo.Api.Tests;

/// <summary>
/// Tests concurrent access patterns to verify cache consistency under load.
/// Simulates real-world scenarios: multiple readers during writes,
/// concurrent writes, and thundering herd after invalidation.
/// </summary>
[Collection("Integration")]
public class ConcurrencyTests
{
    private readonly DemoApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ConcurrencyTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentReads_AllReturnConsistentData()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Warm cache
        await client.GetAsync("/api/customers");

        // 20 concurrent reads
        var tasks = Enumerable.Range(0, 20).Select(_ =>
            client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions));

        var results = await Task.WhenAll(tasks);

        var expectedCount = results[0]!.Length;
        foreach (var result in results)
            result!.Length.Should().Be(expectedCount);
    }

    [Fact]
    public async Task ReadsDuringWrite_CacheConsistentAfterWrite()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        var before = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var countBefore = before!.Length;

        // Start concurrent reads
        var reads = Enumerable.Range(0, 10).Select(_ =>
            client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions));

        // Write in parallel
        var writeTask = client.PostAsJsonAsync("/api/goods",
            new { Name = $"Concurrent-{Guid.NewGuid():N}", Price = 55.00m, Category = "Electronics" });

        await Task.WhenAll(reads);
        var writeResponse = await writeTask;
        writeResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // After write, fresh reads should include new item
        var after = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        after!.Length.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task ThunderingHerd_AfterInvalidation_AllGetFreshData()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-b");

        await client.GetAsync("/api/goods");

        // Create new item (invalidates cache)
        await client.PostAsJsonAsync("/api/goods",
            new { Name = $"Herd-{Guid.NewGuid():N}", Price = 11.00m, Category = "Electronics" });

        // 20 concurrent reads after invalidation
        var tasks = Enumerable.Range(0, 20).Select(_ =>
            client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions));

        var results = await Task.WhenAll(tasks);

        var counts = results.Select(r => r!.Length).Distinct().ToList();
        counts.Should().HaveCount(1, "all concurrent reads should see the same fresh data");
    }

    [Fact]
    public async Task ConcurrentWrites_AllSucceed()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-b");

        var before = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var countBefore = before!.Length;

        // 5 concurrent writes
        var writes = Enumerable.Range(0, 5).Select(i =>
            client.PostAsJsonAsync("/api/goods",
                new { Name = $"Batch-{i}-{Guid.NewGuid():N}", Price = 10.00m + i, Category = "Stationery" }));

        var responses = await Task.WhenAll(writes);
        foreach (var r in responses)
            r.StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        after!.Length.Should().Be(countBefore + 5);
    }

    [Fact]
    public async Task ConcurrentWritesDifferentTenants_NoInterference()
    {
        await _factory.ResetCacheAsync();
        var clientA = _factory.CreateClientForTenant("tenant-a");
        var clientB = _factory.CreateClientForTenant("tenant-b");

        var beforeA = await clientA.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var beforeB = await clientB.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);

        var tasks = new[]
        {
            clientA.PostAsJsonAsync("/api/customers",
                new { Name = $"ConcA-{Guid.NewGuid():N}", Email = $"a-{Guid.NewGuid():N}@test.com" }),
            clientB.PostAsJsonAsync("/api/customers",
                new { Name = $"ConcB-{Guid.NewGuid():N}", Email = $"b-{Guid.NewGuid():N}@test.com" }),
        };

        var responses = await Task.WhenAll(tasks);
        foreach (var r in responses)
            r.StatusCode.Should().Be(HttpStatusCode.Created);

        var afterA = await clientA.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var afterB = await clientB.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);

        afterA!.Length.Should().Be(beforeA!.Length + 1);
        afterB!.Length.Should().Be(beforeB!.Length + 1);
    }

    [Fact]
    public async Task ConcurrentOrderCreation_TransactionsIsolated()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var customerId = customers![0].GetProperty("id").GetString()!;
        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var goodId = goods![0].GetProperty("id").GetString()!;

        var before = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var countBefore = before!.Length;

        // 3 concurrent order creations
        var orderTasks = Enumerable.Range(0, 3).Select(_ =>
            client.PostAsJsonAsync("/api/orders", new
            {
                CustomerId = customerId,
                Items = new[] { new { GoodId = goodId, Quantity = 1 } }
            }));

        var responses = await Task.WhenAll(orderTasks);
        foreach (var r in responses)
            r.StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        after!.Length.Should().Be(countBefore + 3);
    }

    [Fact]
    public async Task ReadersAndWriterAlternating_CacheAlwaysEventuallyConsistent()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-b");

        var countBefore = (await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions))!.Length;

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/goods",
                new { Name = $"Alt-{i}-{Guid.NewGuid():N}", Price = 1.00m, Category = "Stationery" });

            var current = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
            current!.Length.Should().Be(countBefore + i + 1,
                $"after write #{i + 1}, list should include all {i + 1} new items");
        }
    }
}
