using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Api.Tests;

/// <summary>
///     One test per demo scenario: each documents a CachedQueries feature (or pitfall) end to end.
/// </summary>
[Collection("Integration")]
public class FeatureShowcaseTests(DemoApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string Unique(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    private async Task<string> CreateGoodAsync(HttpClient client, string name, decimal price, string category)
    {
        var response = await client.PostAsJsonAsync("/api/goods", new { Name = name, Price = price, Category = category });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;
    }

    private async Task<string> CreateCustomerAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { Name = name, Email = $"{name}@test.com" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetString()!;
    }

    /// <summary>
    ///     Changes data behind the library's back (like another application or a DBA would).
    /// </summary>
    private async Task ExecuteSqlAsync(FormattableString sql)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(sql);
    }

    private static decimal PriceOf(JsonElement[] goods, string id)
    {
        return goods.Single(g => g.GetProperty("id").GetString() == id).GetProperty("price").GetDecimal();
    }

    // --- Fixed demo bugs -------------------------------------------------------------------------

    [Fact]
    public async Task ByCategory_WithEntityTag_SeesNewGoodsWithoutManualInvalidation()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var category = Unique("cat");
        await CreateGoodAsync(client, "first", 10, category);
        (await client.GetFromJsonAsync<JsonElement[]>($"/api/goods/by-category/{category}", JsonOptions))!
            .Should().HaveCount(1);

        await CreateGoodAsync(client, "second", 20, category);

        (await client.GetFromJsonAsync<JsonElement[]>($"/api/goods/by-category/{category}", JsonOptions))!
            .Should().HaveCount(2, "CacheTags.ForEntity<Good>() keeps SaveChanges invalidation next to custom tags");
    }

    [Fact]
    public async Task OrdersList_ManualTagInvalidation_PicksUpExternalChange()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var orders = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        var order = orders![0];
        var id = Guid.Parse(order.GetProperty("id").GetString()!);
        var newStatus = order.GetProperty("status").GetInt32() == 3 ? 2 : 3;

        await ExecuteSqlAsync($"""UPDATE "Orders" SET "Status" = {newStatus} WHERE "Id" = {id}""");

        var cached = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        cached!.Single(o => o.GetProperty("id").GetString() == id.ToString()).GetProperty("status").GetInt32()
            .Should().NotBe(newStatus, "raw SQL is not observed by CachedQueries");

        (await client.PostAsync("/api/orders/invalidate", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var fresh = await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions);
        fresh!.Single(o => o.GetProperty("id").GetString() == id.ToString()).GetProperty("status").GetInt32()
            .Should().Be(newStatus);
    }

    // --- Query options ---------------------------------------------------------------------------

    [Fact]
    public async Task SkipIf_FreshQueryBypassesCache()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var id = await CreateGoodAsync(client, Unique("good"), 10, Unique("cat"));
        await client.GetAsync("/api/goods"); // warm

        await ExecuteSqlAsync($"""UPDATE "Goods" SET "Price" = 11 WHERE "Id" = {Guid.Parse(id)}""");

        PriceOf((await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions))!, id).Should().Be(10);
        PriceOf((await client.GetFromJsonAsync<JsonElement[]>("/api/goods?fresh=true", JsonOptions))!, id)
            .Should().Be(11);
    }

    [Fact]
    public async Task IgnoreContext_CategoriesAreSharedAndInvalidatedByAnyTenant()
    {
        var clientA = factory.CreateClientForTenant("tenant-a");
        var clientB = factory.CreateClientForTenant("tenant-b");
        var category = Unique("global-cat");
        await clientB.GetAsync("/api/goods/categories"); // warm the single global entry

        await CreateGoodAsync(clientA, "x", 1, category);

        var seenByB = await clientB.GetFromJsonAsync<string[]>("/api/goods/categories", JsonOptions);
        seenByB.Should().Contain(category, "the global entry is invalidated by any tenant's Good changes");
    }

    [Fact]
    public async Task SingleOrDefault_FindsCustomerByEmail()
    {
        var client = factory.CreateClientForTenant("tenant-a");

        var found = await client.GetFromJsonAsync<JsonElement>("/api/customers/by-email?email=customer1@tenant-a.com",
            JsonOptions);
        found.GetProperty("email").GetString().Should().Be("customer1@tenant-a.com");

        (await client.GetAsync("/api/customers/by-email?email=customer1@tenant-b.com")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "other tenants' customers are not visible");
    }

    // --- Dependencies & invalidation -------------------------------------------------------------

    [Fact]
    public async Task Subquery_NewOrderInvalidatesCustomersWithOrders()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var customerId = await CreateCustomerAsync(client, Unique("buyer"));
        var goodId = await CreateGoodAsync(client, Unique("good"), 5, Unique("cat"));

        (await client.GetFromJsonAsync<JsonElement[]>("/api/customers/with-orders", JsonOptions))!
            .Should().NotContain(c => c.GetProperty("id").GetString() == customerId);

        var order = await client.PostAsJsonAsync("/api/orders",
            new { CustomerId = customerId, Items = new[] { new { GoodId = goodId, Quantity = 1 } } });
        order.StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.GetFromJsonAsync<JsonElement[]>("/api/customers/with-orders", JsonOptions))!
            .Should().Contain(c => c.GetProperty("id").GetString() == customerId,
                "the list depends on Orders through its subquery");
    }

    [Fact]
    public async Task Update_InvalidatesAutoTrackedQueriesAndCustomKeyEntry()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var id = await CreateCustomerAsync(client, Unique("before"));
        await client.GetAsync($"/api/customers/{id}"); // cached under custom key
        await client.GetAsync("/api/customers"); // cached, auto-tracked
        var newName = Unique("after");

        var update = await client.PutAsJsonAsync($"/api/customers/{id}", new { Name = newName, Email = "x@test.com" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetFromJsonAsync<JsonElement>($"/api/customers/{id}", JsonOptions))
            .GetProperty("name").GetString().Should().Be(newName);
        (await client.GetFromJsonAsync<JsonElement[]>("/api/customers", JsonOptions))!
            .Should().Contain(c => c.GetProperty("name").GetString() == newName);
    }

    [Fact]
    public async Task InvalidateByKeys_RefreshesSeveralCustomers()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var ids = new[] { await CreateCustomerAsync(client, Unique("k1")), await CreateCustomerAsync(client, Unique("k2")) };
        foreach (var id in ids)
        {
            await client.GetAsync($"/api/customers/{id}");
            await ExecuteSqlAsync($"""UPDATE "Customers" SET "Name" = 'renamed' WHERE "Id" = {Guid.Parse(id)}""");
        }

        (await client.PostAsJsonAsync("/api/customers/invalidate", ids)).StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var id in ids)
        {
            (await client.GetFromJsonAsync<JsonElement>($"/api/customers/{id}", JsonOptions))
                .GetProperty("name").GetString().Should().Be("renamed");
        }
    }

    [Fact]
    public async Task InvalidateByTags_RefreshesSeveralCategories()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var categories = new[] { Unique("c1"), Unique("c2") };
        var ids = new List<string>();
        foreach (var category in categories)
        {
            ids.Add(await CreateGoodAsync(client, "g", 10, category));
            await client.GetAsync($"/api/goods/by-category/{category}"); // warm
        }

        foreach (var id in ids)
        {
            await ExecuteSqlAsync($"""UPDATE "Goods" SET "Price" = 42 WHERE "Id" = {Guid.Parse(id)}""");
        }

        (await client.PostAsJsonAsync("/api/goods/invalidate-categories", categories)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        foreach (var category in categories)
        {
            (await client.GetFromJsonAsync<JsonElement[]>($"/api/goods/by-category/{category}", JsonOptions))!
                .Single().GetProperty("price").GetDecimal().Should().Be(42);
        }
    }

    [Fact]
    public async Task ExecuteUpdate_RequiresManualInvalidation()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var category = Unique("bulk");
        await CreateGoodAsync(client, "g", 100, category);
        await client.GetAsync($"/api/goods/by-category/{category}"); // warm

        await client.PostAsJsonAsync("/api/goods/reprice",
            new { Category = category, Percent = 10, InvalidateCache = false });
        (await client.GetFromJsonAsync<JsonElement[]>($"/api/goods/by-category/{category}", JsonOptions))!
            .Single().GetProperty("price").GetDecimal().Should().Be(100, "ExecuteUpdate bypasses SaveChanges");

        await client.PostAsJsonAsync("/api/goods/reprice", new { Category = category, Percent = 10 });
        (await client.GetFromJsonAsync<JsonElement[]>($"/api/goods/by-category/{category}", JsonOptions))!
            .Single().GetProperty("price").GetDecimal().Should().Be(121);
    }

    // --- Transactions ----------------------------------------------------------------------------

    [Fact]
    public async Task TransactionScope_CommittedOrderInvalidatesOrdersList()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var customerId = await CreateCustomerAsync(client, Unique("scoped"));
        var goodId = await CreateGoodAsync(client, Unique("good"), 3, Unique("cat"));
        var before = (await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions))!.Length;

        var response = await client.PostAsJsonAsync("/api/orders/scoped",
            new { CustomerId = customerId, Items = new[] { new { GoodId = goodId, Quantity = 2 } } });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions))!.Length.Should().Be(before + 1);
    }

    [Fact]
    public async Task TransactionScope_RejectedOrderLeavesOrdersListUntouched()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var before = (await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions))!.Length;

        var response = await client.PostAsJsonAsync("/api/orders/scoped",
            new { CustomerId = Guid.NewGuid(), Items = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<JsonElement[]>("/api/orders", JsonOptions))!.Length.Should().Be(before);
    }

    // --- Background services ---------------------------------------------------------------------

    private async Task RunPriceJobAsync(HttpClient client, string category, bool explicitInvalidation)
    {
        var response = await client.PostAsJsonAsync("/api/jobs/price-adjustment",
            new { Category = category, Percent = 50, ExplicitInvalidation = explicitInvalidation });
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("jobId").GetString();

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var status = await client.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}", JsonOptions);
            var state = status.GetProperty("state").GetString();
            if (state != "Queued")
            {
                state.Should().Be("Completed");
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Price adjustment job did not complete");
    }

    [Fact]
    public async Task BackgroundJob_WithExplicitTenantInvalidation_RefreshesTenantCache()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var id = await CreateGoodAsync(client, Unique("good"), 10, Unique("job"));
        var category = (await client.GetFromJsonAsync<JsonElement>($"/api/goods/{id}", JsonOptions))
            .GetProperty("category").GetString()!;
        await client.GetAsync("/api/goods"); // warm the tenant's list

        await RunPriceJobAsync(client, category, explicitInvalidation: true);

        PriceOf((await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions))!, id).Should().Be(15);
    }

    [Fact]
    public async Task BackgroundJob_WithoutExplicitInvalidation_LeavesTenantCacheStale()
    {
        var client = factory.CreateClientForTenant("tenant-a");
        var id = await CreateGoodAsync(client, Unique("good"), 10, Unique("job"));
        var category = (await client.GetFromJsonAsync<JsonElement>($"/api/goods/{id}", JsonOptions))
            .GetProperty("category").GetString()!;
        await client.GetAsync("/api/goods"); // warm the tenant's list

        await RunPriceJobAsync(client, category, explicitInvalidation: false);

        PriceOf((await client.GetFromJsonAsync<JsonElement[]>("/api/goods", JsonOptions))!, id)
            .Should().Be(10, "SaveChanges without a tenant context only invalidates global entries");
        PriceOf((await client.GetFromJsonAsync<JsonElement[]>("/api/goods?fresh=true", JsonOptions))!, id)
            .Should().Be(15);
    }
}
