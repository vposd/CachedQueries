using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Demo.Api.Tests;

[Collection("Integration")]
public class CustomersTests
{
    private readonly DemoApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CustomersTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCustomers_ReturnsSeedData()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);

        customers.Should().NotBeNull();
        customers!.Length.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GetCustomers_CachedResult_ReturnsSameData()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        var first = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var second = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);

        first!.Length.Should().Be(second!.Length);
    }

    [Fact]
    public async Task GetCustomerById_ReturnsCustomer()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var id = customers![0].GetProperty("id").GetString();

        var response = await client.GetAsync($"/api/customers/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var customer = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        customer.GetProperty("id").GetString().Should().Be(id);
    }

    [Fact]
    public async Task GetCustomerById_NotFound_Returns404()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var response = await client.GetAsync($"/api/customers/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CustomerExists_ReturnsTrueForExistingEmail()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var response = await client.GetFromJsonAsync<JsonElement>(
            "/api/customers/exists?email=customer1@tenant-a.com", JsonOptions);

        response.GetProperty("exists").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CustomerExists_ReturnsFalseForNonExistingEmail()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var response = await client.GetFromJsonAsync<JsonElement>(
            "/api/customers/exists?email=nobody@nowhere.com", JsonOptions);

        response.GetProperty("exists").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CreateCustomer_ReturnsCreated()
    {
        var client = _factory.CreateClientForTenant("tenant-a");

        var response = await client.PostAsJsonAsync("/api/customers",
            new { Name = $"Test-{Guid.NewGuid():N}", Email = $"test-{Guid.NewGuid():N}@test.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateCustomer_AutoInvalidatesListCache()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Warm cache (miss → DB → SET → register)
        var before = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var countBefore = before!.Length;

        // Create (SaveChanges → invalidate → remove from Redis)
        await client.PostAsJsonAsync("/api/customers",
            new { Name = $"Inv-{Guid.NewGuid():N}", Email = $"inv-{Guid.NewGuid():N}@test.com" });

        // Should be a cache miss → fresh data
        var after = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        after!.Length.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task InvalidateByKey_InvalidatesCachedCustomer()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Get all customers to find one with a known ID
        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var id = customers![0].GetProperty("id").GetString();
        var originalName = customers[0].GetProperty("name").GetString();

        // Warm the single-customer cache (uses WithKey("customer:{id}"))
        var cached = await client.GetFromJsonAsync<JsonElement>($"/api/customers/{id}", JsonOptions);
        cached.GetProperty("name").GetString().Should().Be(originalName);

        // Invalidate by key
        var invalidateResponse = await client.PostAsync($"/api/customers/{id}/invalidate", null);
        invalidateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Should still return data (cache miss → fresh from DB)
        var afterInvalidation = await client.GetAsync($"/api/customers/{id}");
        afterInvalidation.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvalidateByKey_DoesNotAffectOtherCustomerCaches()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Get two distinct customer IDs
        var customers = await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        customers!.Length.Should().BeGreaterOrEqualTo(2);
        var id1 = customers[0].GetProperty("id").GetString();
        var id2 = customers[1].GetProperty("id").GetString();

        // Warm cache for both
        await client.GetAsync($"/api/customers/{id1}");
        await client.GetAsync($"/api/customers/{id2}");

        // Invalidate only the first
        await client.PostAsync($"/api/customers/{id1}/invalidate", null);

        // Second customer should still be accessible
        var response = await client.GetAsync($"/api/customers/{id2}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var customer2 = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        customer2.GetProperty("id").GetString().Should().Be(id2);
    }

    [Fact]
    public async Task InvalidateByKey_TenantIsolation_DoesNotAffectOtherTenant()
    {
        await _factory.ResetCacheAsync();
        var clientA = _factory.CreateClientForTenant("tenant-a");
        var clientB = _factory.CreateClientForTenant("tenant-b");

        // Get a customer from each tenant
        var customersA = await clientA.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var customersB = await clientB.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var idA = customersA![0].GetProperty("id").GetString();
        var idB = customersB![0].GetProperty("id").GetString();

        // Warm cache for both tenants
        await clientA.GetAsync($"/api/customers/{idA}");
        await clientB.GetAsync($"/api/customers/{idB}");

        // Invalidate tenant-a's customer
        await clientA.PostAsync($"/api/customers/{idA}/invalidate", null);

        // Tenant-b's customer should still be cached and accessible
        var response = await clientB.GetAsync($"/api/customers/{idB}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var customerB = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        customerB.GetProperty("id").GetString().Should().Be(idB);
    }

    [Fact]
    public async Task ClearTenantCache_ReturnsOk()
    {
        var client = _factory.CreateClientForTenant("tenant-a");
        await client.GetAsync("/api/customers");

        var response = await client.PostAsync("/api/customers/clear-cache", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
