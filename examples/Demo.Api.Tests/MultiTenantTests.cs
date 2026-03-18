using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Demo.Api.Tests;

[Collection("Integration")]
public class MultiTenantTests
{
    private readonly DemoApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MultiTenantTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantA_OnlySeesOwnCustomers()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);

        customers.Should().NotBeNull();
        foreach (var c in customers!)
            c.GetProperty("tenantId").GetString().Should().Be("tenant-a");
    }

    [Fact]
    public async Task TenantB_OnlySeesOwnCustomers()
    {
        var client = _factory.CreateClientForTenant("tenant-b");

        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);

        customers.Should().NotBeNull();
        foreach (var c in customers!)
            c.GetProperty("tenantId").GetString().Should().Be("tenant-b");
    }

    [Fact]
    public async Task DifferentTenants_HaveSeparateGoods()
    {
        var clientA = _factory.CreateClientForTenant("tenant-a");
        var clientB = _factory.CreateClientForTenant("tenant-b");

        var goodsA = await clientA.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var goodsB = await clientB.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);

        var idsA = goodsA!.Select(g => g.GetProperty("id").GetString()).ToHashSet();
        var idsB = goodsB!.Select(g => g.GetProperty("id").GetString()).ToHashSet();

        idsA.Intersect(idsB).Should().BeEmpty("tenants should have completely separate goods");
    }

    [Fact]
    public async Task CreateInTenantA_DoesNotAppearInTenantB()
    {
        var clientA = _factory.CreateClientForTenant("tenant-a");
        var clientB = _factory.CreateClientForTenant("tenant-b");

        // Warm tenant B
        var goodsBBefore = await clientB.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var countBBefore = goodsBBefore!.Length;

        // Create in tenant A
        await clientA.PostAsJsonAsync("/api/goods",
            new { Name = $"TenantA-Only-{Guid.NewGuid():N}", Price = 10.00m, Category = "Electronics" });

        // Tenant B unaffected (even after A's cache invalidation)
        var goodsBAfter = await clientB.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        goodsBAfter!.Length.Should().Be(countBBefore);
    }

    [Fact]
    public async Task ClearTenantACache_DoesNotAffectTenantB()
    {
        var clientA = _factory.CreateClientForTenant("tenant-a");
        var clientB = _factory.CreateClientForTenant("tenant-b");

        // Warm both
        await clientA.GetAsync("/api/customers");
        await clientB.GetAsync("/api/customers");

        // Clear only tenant A
        await clientA.PostAsync("/api/customers/clear-cache", null);

        // Tenant B still works fine
        var response = await clientB.GetAsync("/api/customers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var customers = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);
        customers!.Length.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task DefaultTenant_UsedWhenNoHeader()
    {
        var client = _factory.CreateClient();

        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        customers!.Length.Should().Be(0, "default tenant has no seeded data");
    }

    [Fact]
    public async Task TenantIsolation_OrdersArePerTenant()
    {
        var clientA = _factory.CreateClientForTenant("tenant-a");
        var clientB = _factory.CreateClientForTenant("tenant-b");

        var ordersA = await clientA.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var ordersB = await clientB.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);

        var idsA = ordersA!.Select(o => o.GetProperty("id").GetString()).ToHashSet();
        var idsB = ordersB!.Select(o => o.GetProperty("id").GetString()).ToHashSet();

        idsA.Intersect(idsB).Should().BeEmpty("tenants should have separate orders");
    }

    [Fact]
    public async Task TenantIsolation_GoodsCountIsPerTenant()
    {
        var clientA = _factory.CreateClientForTenant("tenant-a");
        var clientB = _factory.CreateClientForTenant("tenant-b");

        var countA = (await clientA.GetFromJsonAsync<JsonElement>("/api/goods/count", JsonOptions))
            .GetProperty("count").GetInt32();
        var countB = (await clientB.GetFromJsonAsync<JsonElement>("/api/goods/count", JsonOptions))
            .GetProperty("count").GetInt32();

        countA.Should().BeGreaterOrEqualTo(5);
        countB.Should().BeGreaterOrEqualTo(5);
    }
}
