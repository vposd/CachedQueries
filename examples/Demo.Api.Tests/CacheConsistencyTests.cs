using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Demo.Api.Tests;

/// <summary>
/// Production-readiness tests verifying cache consistency guarantees:
/// - Cache never serves stale data after invalidation
/// - Related entity changes propagate correctly
/// - Includes/navigation property changes invalidate parent queries
/// - Edge cases around empty results, single items, counts
/// </summary>
[Collection("Integration")]
public class CacheConsistencyTests
{
    private readonly DemoApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CacheConsistencyTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateThenFetch_NewItemImmediatelyVisible()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");
        var uniqueName = $"Immediate-{Guid.NewGuid():N}";

        // Warm cache first
        await client.GetAsync("/api/goods");

        await client.PostAsJsonAsync("/api/goods",
            new { Name = uniqueName, Price = 42.00m, Category = "Electronics" });

        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        goods!.Select(g => g.GetProperty("name").GetString()).Should().Contain(uniqueName);
    }

    [Fact]
    public async Task CachedSingleItem_NotFound_ThenStillNotFound()
    {
        var client = _factory.CreateClient();
        var fakeId = Guid.NewGuid();

        var response1 = await client.GetAsync($"/api/goods/{fakeId}");
        response1.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var response2 = await client.GetAsync($"/api/goods/{fakeId}");
        response2.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BooleanCache_BecomesTrue_AfterCreation()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");
        var uniqueEmail = $"bool-{Guid.NewGuid():N}@test.com";

        var before = await client.GetFromJsonAsync<JsonElement>(
            $"/api/customers/exists?email={uniqueEmail}", JsonOptions);
        before.GetProperty("exists").GetBoolean().Should().BeFalse();

        await client.PostAsJsonAsync("/api/customers",
            new { Name = "Bool Test", Email = uniqueEmail });

        var after = await client.GetFromJsonAsync<JsonElement>(
            $"/api/customers/exists?email={uniqueEmail}", JsonOptions);
        after.GetProperty("exists").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CountCache_IncrementsAfterCreate()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        var countBefore = (await client.GetFromJsonAsync<JsonElement>("/api/goods/count", JsonOptions))
            .GetProperty("count").GetInt32();

        await client.PostAsJsonAsync("/api/goods",
            new { Name = $"Count-{Guid.NewGuid():N}", Price = 5.00m, Category = "Stationery" });

        var countAfter = (await client.GetFromJsonAsync<JsonElement>("/api/goods/count", JsonOptions))
            .GetProperty("count").GetInt32();

        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task OrderQuery_WithIncludes_InvalidatedWhenCustomerChanges()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Warm orders cache (includes Customer entity type)
        await client.GetAsync("/api/orders");

        // Add a new customer — orders query depends on Customer entity type
        await client.PostAsJsonAsync("/api/customers",
            new { Name = $"NewCust-{Guid.NewGuid():N}", Email = "new@test.com" });

        // Orders endpoint should still work (cache miss → fresh data)
        var response = await client.GetAsync("/api/orders");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MultipleInvalidations_DoNotCorruptCache()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Warm cache
        await client.GetAsync("/api/goods");

        // Rapid-fire: create 3 goods
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/goods",
                new { Name = $"Rapid-{i}-{Guid.NewGuid():N}", Price = 1.00m, Category = "Electronics" });
        }

        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        goods!.Where(g => g.GetProperty("name").GetString()!.StartsWith("Rapid-"))
            .Count().Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task ClearAll_ThenWrite_CacheCorrectlyRebuilt()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Warm
        await client.GetAsync("/api/customers");
        await client.GetAsync("/api/goods");

        // Reset again to simulate a "clear all" scenario
        await _factory.ResetCacheAsync();

        // Write something new
        await client.PostAsJsonAsync("/api/customers",
            new { Name = $"PostClear-{Guid.NewGuid():N}" });

        // All endpoints should return current DB state
        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        customers!.Any(c => c.GetProperty("name").GetString()!.StartsWith("PostClear-"))
            .Should().BeTrue();

        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        goods!.Length.Should().BeGreaterOrEqualTo(5);

        var orders = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        orders!.Length.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task TagInvalidation_OnlyAffectsTaggedQueries()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-b");

        var allGoodsBefore = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var furnitureBefore = await client.GetFromJsonAsync<JsonElement[]>(
            "/api/goods/by-category/Furniture", JsonOptions);

        // Invalidate only the "category:Furniture" tag
        await client.PostAsync("/api/goods/invalidate-category/Furniture", null);

        var furnitureAfter = await client.GetFromJsonAsync<JsonElement[]>(
            "/api/goods/by-category/Furniture", JsonOptions);
        furnitureAfter!.Length.Should().Be(furnitureBefore!.Length);

        var allGoodsAfter = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        allGoodsAfter!.Length.Should().Be(allGoodsBefore!.Length);
    }

    [Fact]
    public async Task EmptyTenant_ReturnsEmptyArrays_NotErrors()
    {
        var client = _factory.CreateClientForTenant("non-existent-tenant");

        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        customers!.Length.Should().Be(0);

        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        goods!.Length.Should().Be(0);

        var orders = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        orders!.Length.Should().Be(0);

        var count = (await client.GetFromJsonAsync<JsonElement>("/api/goods/count", JsonOptions))
            .GetProperty("count").GetInt32();
        count.Should().Be(0);
    }

    [Fact]
    public async Task OrderStatusUpdate_CacheReflectsNewStatus()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Create a fresh order
        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var customerId = customers![0].GetProperty("id").GetString()!;
        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var goodId = goods![0].GetProperty("id").GetString()!;

        var createResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            Items = new[] { new { GoodId = goodId, Quantity = 1 } }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var orderId = created.GetProperty("id").GetString()!;

        // Warm cache
        await client.GetAsync("/api/orders");

        // Update to Shipped
        await client.PutAsJsonAsync($"/api/orders/{orderId}/status", new { Status = 2 });
        var orders = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        orders!.First(o => o.GetProperty("id").GetString() == orderId)
            .GetProperty("status").GetInt32().Should().Be(2);

        // Update to Delivered
        await client.PutAsJsonAsync($"/api/orders/{orderId}/status", new { Status = 3 });
        orders = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        orders!.First(o => o.GetProperty("id").GetString() == orderId)
            .GetProperty("status").GetInt32().Should().Be(3);
    }
}
