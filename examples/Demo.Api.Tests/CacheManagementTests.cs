using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Demo.Api.Tests;

[Collection("Integration")]
public class CacheManagementTests
{
    private readonly DemoApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CacheManagementTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ClearAll_ReturnsOk()
    {
        var client = _factory.CreateClientForTenant("tenant-a");
        await client.GetAsync("/api/customers"); // warm

        var response = await client.PostAsync("/api/cache/clear-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ClearAll_DataStillAccessibleAfterClear()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        await client.GetAsync("/api/customers"); // warm
        await client.PostAsync("/api/cache/clear-all", null);

        // Data comes from DB on cache miss
        var response = await client.GetAsync("/api/customers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var customers = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);
        customers!.Length.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task ClearTenant_ReturnsOk()
    {
        var client = _factory.CreateClientForTenant("tenant-b");
        await client.GetAsync("/api/goods"); // warm

        var response = await client.PostAsync("/api/cache/clear-tenant", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvalidateEntity_Customer_ReturnsOk()
    {
        var client = _factory.CreateClientForTenant("tenant-a");
        await client.GetAsync("/api/customers"); // warm

        var response = await client.PostAsync("/api/cache/invalidate-entity/customer", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvalidateEntity_AcceptsAllEntityTypes()
    {
        var client = _factory.CreateClient();

        foreach (var entity in new[] { "goods", "orders", "orderitems" })
        {
            var response = await client.PostAsync($"/api/cache/invalidate-entity/{entity}", null);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"entity type '{entity}' should be valid");
        }
    }

    [Fact]
    public async Task InvalidateEntity_Unknown_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/cache/invalidate-entity/nonexistent", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidateEntity_DataStillAccessibleAfterInvalidation()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        await client.GetAsync("/api/goods"); // warm
        await client.PostAsync("/api/cache/invalidate-entity/goods", null);

        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        goods!.Length.Should().BeGreaterOrEqualTo(5);
    }
}
