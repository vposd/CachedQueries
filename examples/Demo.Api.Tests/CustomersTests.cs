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
    public async Task ClearTenantCache_ReturnsOk()
    {
        var client = _factory.CreateClientForTenant("tenant-a");
        await client.GetAsync("/api/customers");

        var response = await client.PostAsync("/api/customers/clear-cache", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
