using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Demo.Api.Tests;

[Collection("Integration")]
public class OrdersTests
{
    private readonly DemoApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OrdersTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOrders_ReturnsSeedOrders()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var response = await client.GetAsync("/api/orders");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);
        orders!.Length.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task GetOrders_IncludesCustomerAndItems()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var orders = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var order = orders![0];
        order.GetProperty("customer").GetString().Should().NotBeNullOrEmpty();
        order.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        order.GetProperty("total").GetDecimal().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetOrders_CachedResult_ReturnsSameData()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        var first = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var second = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);

        first!.Length.Should().Be(second!.Length);
    }

    [Fact]
    public async Task CreateOrder_InTransaction_ReturnsCreated()
    {
        var client = _factory.CreateClientForTenant("tenant-a");
        var (customerId, goodId) = await GetTestIds(client);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            Items = new[] { new { GoodId = goodId, Quantity = 3 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateOrder_InvalidatesOrdersListCache()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        var before = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var countBefore = before!.Length;

        var (customerId, goodId) = await GetTestIds(client);
        await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            Items = new[] { new { GoodId = goodId, Quantity = 1 } }
        });

        var after = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        after!.Length.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateOrder_InvalidCustomer_ReturnsBadRequest()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = Guid.NewGuid(),
            Items = new[] { new { GoodId = Guid.NewGuid(), Quantity = 1 } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_InvalidGood_ReturnsBadRequest()
    {
        var client = _factory.CreateClientForTenant("tenant-a");
        var (customerId, _) = await GetTestIds(client);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            Items = new[] { new { GoodId = Guid.NewGuid(), Quantity = 1 } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateOrderStatus_ReturnsUpdatedStatus()
    {
        var client = _factory.CreateClientForTenant("tenant-a");
        var orders = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var orderId = orders![0].GetProperty("id").GetString();

        var response = await client.PutAsJsonAsync($"/api/orders/{orderId}/status",
            new { Status = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("status").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task UpdateOrderStatus_InvalidatesCache()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Create a fresh order
        var (customerId, goodId) = await GetTestIds(client);
        var createResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            Items = new[] { new { GoodId = goodId, Quantity = 1 } }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var orderId = created.GetProperty("id").GetString();

        // Warm cache
        await client.GetAsync("/api/orders");

        // Update status to Delivered (3)
        await client.PutAsJsonAsync($"/api/orders/{orderId}/status", new { Status = 3 });

        // Cache should be invalidated
        var orders = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var updated = orders!.First(o => o.GetProperty("id").GetString() == orderId);
        updated.GetProperty("status").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task UpdateOrderStatus_NotFound_Returns404()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var response = await client.PutAsJsonAsync($"/api/orders/{Guid.NewGuid()}/status",
            new { Status = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ManualInvalidateOrders_ReturnsOk()
    {
        var client = _factory.CreateClientForTenant("tenant-a");
        await client.GetAsync("/api/orders");

        var response = await client.PostAsync("/api/orders/invalidate", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
