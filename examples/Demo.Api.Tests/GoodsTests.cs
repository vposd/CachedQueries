using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Demo.Api.Tests;

[Collection("Integration")]
public class GoodsTests
{
    private readonly DemoApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GoodsTests(DemoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetGoods_ReturnsSeedData()
    {
        var client = _factory.CreateClientForTenant("tenant-b");

        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);

        goods.Should().NotBeNull();
        goods!.Length.Should().BeGreaterOrEqualTo(5);
    }

    [Fact]
    public async Task GetGoods_CachedResult_ReturnsSameData()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-b");

        var first = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var second = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);

        first!.Length.Should().Be(second!.Length);
    }

    [Fact]
    public async Task GetGoodsByCategory_ReturnsFilteredResults()
    {
        var client = _factory.CreateClientForTenant("tenant-b");

        var electronics = await client.GetFromJsonAsync<JsonElement[]>(
            "/api/goods/by-category/Electronics", JsonOptions);

        electronics.Should().NotBeNull();
        electronics!.Length.Should().BeGreaterOrEqualTo(2);
        foreach (var g in electronics)
            g.GetProperty("category").GetString().Should().Be("Electronics");
    }

    [Fact]
    public async Task GetGoodsCount_ReturnsPositive()
    {
        var client = _factory.CreateClientForTenant("tenant-b");

        var result = await client.GetFromJsonAsync<JsonElement>("/api/goods/count", JsonOptions);
        result.GetProperty("count").GetInt32().Should().BeGreaterOrEqualTo(5);
    }

    [Fact]
    public async Task GetGoodById_ReturnsGood()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-b");

        var goods = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var id = goods![0].GetProperty("id").GetString();

        var response = await client.GetAsync($"/api/goods/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var good = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        good.GetProperty("id").GetString().Should().Be(id);
    }

    [Fact]
    public async Task GetGoodById_NotFound_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/goods/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateGood_AutoInvalidatesListCache()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-b");

        var before = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        var countBefore = before!.Length;

        await client.PostAsJsonAsync("/api/goods",
            new { Name = $"Widget-{Guid.NewGuid():N}", Price = 79.99m, Category = "Electronics" });

        var after = await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions);
        after!.Length.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task CreateGood_AutoInvalidatesScalarCache()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-b");

        var countBefore = (await client.GetFromJsonAsync<JsonElement>("/api/goods/count", JsonOptions))
            .GetProperty("count").GetInt32();

        await client.PostAsJsonAsync("/api/goods",
            new { Name = $"Scalar-{Guid.NewGuid():N}", Price = 9.99m, Category = "Stationery" });

        var countAfter = (await client.GetFromJsonAsync<JsonElement>("/api/goods/count", JsonOptions))
            .GetProperty("count").GetInt32();

        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task ManualTagInvalidation_ReturnsOk()
    {
        var client = _factory.CreateClientForTenant("tenant-b");
        await client.GetAsync("/api/goods/by-category/Electronics");

        var response = await client.PostAsync("/api/goods/invalidate-category/Electronics", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateGood_AutoInvalidatesCategoryCache()
    {
        await _factory.ResetCacheAsync();
        var client = _factory.CreateClientForTenant("tenant-b");

        var before = await client.GetFromJsonAsync<JsonElement[]>(
            "/api/goods/by-category/Furniture", JsonOptions);
        var countBefore = before!.Length;

        await client.PostAsJsonAsync("/api/goods",
            new { Name = $"Shelf-{Guid.NewGuid():N}", Price = 149.99m, Category = "Furniture" });

        var after = await client.GetFromJsonAsync<JsonElement[]>(
            "/api/goods/by-category/Furniture", JsonOptions);
        after!.Length.Should().Be(countBefore + 1);
    }
}
