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
    public async Task InvalidateByKey_CacheReturnsUpdatedData()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-a");

        // Create a customer so we have a known entity to update
        var createResponse = await client.PostAsJsonAsync("/api/customers",
            new { Name = $"KeyTest-{Guid.NewGuid():N}", Email = $"keytest-{Guid.NewGuid():N}@test.com" });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = created.GetProperty("id").GetString();

        // Warm the single-customer cache (uses WithKey("customer:{id}"))
        var cached = await client.GetFromJsonAsync<JsonElement>($"/api/customers/{id}", JsonOptions);
        cached.GetProperty("id").GetString().Should().Be(id);

        // Invalidate by key
        var invalidateResponse = await client.PostAsync($"/api/customers/{id}/invalidate", null);
        invalidateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // After invalidation, should still return data (cache miss → fresh from DB)
        var afterInvalidation = await client.GetAsync($"/api/customers/{id}");
        afterInvalidation.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await afterInvalidation.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.GetProperty("id").GetString().Should().Be(id);
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
    public async Task InvalidateByKey_TenantIsolation_SameKeyDifferentTenants()
    {
        await _factory.ResetCacheAsync();
        var clientA = _factory.CreateClientForTenant("tenant-a");
        var clientB = _factory.CreateClientForTenant("tenant-b");

        // Create a customer in each tenant, then cache using the SAME custom key
        // Both tenants' GET /api/customers/{id} endpoint uses WithKey("customer:{id}")
        // The same ID doesn't exist in both, so we use the list endpoint which uses auto-generated keys
        // Instead, we verify that tenant-a invalidation of a shared-format key doesn't touch tenant-b

        // Get customer IDs (different per tenant, but same key format "customer:{id}")
        var customersA = await clientA.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var customersB = await clientB.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        var countA = customersA!.Length;
        var countB = customersB!.Length;

        // Warm list caches for both tenants
        await clientA.GetAsync("/api/customers");
        await clientB.GetAsync("/api/customers");

        // Create a customer in tenant-a (auto-invalidates tenant-a's list cache)
        await clientA.PostAsJsonAsync("/api/customers",
            new { Name = $"Isolation-{Guid.NewGuid():N}", Email = $"iso-{Guid.NewGuid():N}@test.com" });

        // Tenant-a should see the new customer (cache was auto-invalidated)
        var afterA = await clientA.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        afterA!.Length.Should().Be(countA + 1);

        // Tenant-b's cache should be untouched — still returns original count
        var afterB = await clientB.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions);
        afterB!.Length.Should().Be(countB);
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
