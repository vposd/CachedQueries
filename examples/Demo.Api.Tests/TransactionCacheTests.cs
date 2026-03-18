using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Demo.Api.Tests;

[Collection("Integration")]
public class TransactionCacheTests
{
    private readonly DemoApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TransactionCacheTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateOrder_CacheInvalidatedAfterCommit()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        var before = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var countBefore = before!.Length;

        var (customerId, goodId) = await GetTestIds(client);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            Items = new[] { new { GoodId = goodId, Quantity = 2 } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        after!.Length.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateOrder_WithMultipleItems()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var customerId = customers![0].GetProperty("id").GetString()!;
        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);

        var items = goods!.Take(3).Select(g => new
        {
            GoodId = g.GetProperty("id").GetString(),
            Quantity = 1
        }).ToArray();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            Items = items
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateOrder_InvalidGood_RollbackLeavesDataIntact()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        var before = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var countBefore = before!.Length;

        var (customerId, goodId) = await GetTestIds(client);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            Items = new[]
            {
                new { GoodId = goodId, Quantity = 1 },
                new { GoodId = Guid.NewGuid().ToString(), Quantity = 1 }
            }
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var after = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        after!.Length.Should().Be(countBefore);
    }

    [Fact]
    public async Task CreateOrder_InvalidCustomer_DoesNotInvalidateCache()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        var before = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var countBefore = before!.Length;

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = Guid.NewGuid(),
            Items = new[] { new { GoodId = Guid.NewGuid(), Quantity = 1 } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var after = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        after!.Length.Should().Be(countBefore);
    }

    private async Task<(string customerId, string goodId)> GetTestIds(HttpClient client)
    {
        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var customerId = customers![0].GetProperty("id").GetString()!;
        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var goodId = goods![0].GetProperty("id").GetString()!;
        return (customerId, goodId);
    }
}
