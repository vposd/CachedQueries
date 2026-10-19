using System.Text.Json.Serialization;
using CachedQueries;
using CachedQueries.Extensions;
using CachedQueries.Redis;
using Demo.Api;
using Demo.Api.Endpoints;
using Demo.Api.Entities;
using Demo.Api.Jobs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- JSON serialization ---
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// --- EF Core with PostgreSQL ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// --- CachedQueries setup ---
builder.Services.AddHttpContextAccessor();

void ConfigureCache(CachedQueriesConfiguration config)
{
    // Default expiration for .Cacheable() / .Cacheable(o => ...) without Expire()/SlidingExpiration()
    config.DefaultOptions = new CachingOptions(TimeSpan.FromMinutes(30));

    // Auto-invalidate cache on SaveChanges (default: true)
    config.AutoInvalidation = true;

    // Multi-tenant cache isolation (see TenantCacheContextProvider)
    config.UseContextProvider<TenantCacheContextProvider>();
}

// "Cache:Provider" = "Memory" (default, single instance) or "Redis" (distributed, see docker-compose.yml)
if (string.Equals(builder.Configuration["Cache:Provider"], "Redis", StringComparison.OrdinalIgnoreCase))
{
    // One shared connection, atomic Lua-based tag operations
    builder.Services.AddCachedQueriesWithRedis(builder.Configuration.GetConnectionString("Redis")!, ConfigureCache);

    // Different backends per query type are possible too, e.g. keeping scalar results (Count/Any) in a
    // second provider. Only do this with providers that every instance shares — an in-process cache per
    // instance would not see invalidations made by other instances.
    //
    // builder.Services.AddCachedQueriesWithRedis(connectionString, config => config
    //     .UseCollectionProvider<RedisCacheProvider>()
    //     .UseSingleItemProvider<RedisCacheProvider>()
    //     .UseScalarProvider<MyOtherSharedProvider>());
    //
    // ...and per query: .Cacheable(o => o.UseTarget(CacheTarget.Scalar))
}
else
{
    builder.Services.AddCachedQueries(ConfigureCache);
}

// Wire up cache invalidation interceptors to DbContext (must come after AddDbContext)
builder.Services.AddCacheInvalidation<AppDbContext>();

// --- Background jobs (tenant data changed outside HTTP requests) ---
builder.Services.AddSingleton<PriceAdjustmentQueue>();
builder.Services.AddHostedService<PriceAdjustmentWorker>();

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CachedQueries Demo", Version = "v1" });
});

var app = builder.Build();

// Initialize CachedQueries static accessor
app.Services.UseCachedQueries();

// --- Auto-migrate & seed ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData(db);
}

app.UseSwagger();
app.UseSwaggerUI();

// --- Map endpoints ---
app.MapCustomersEndpoints();
app.MapGoodsEndpoints();
app.MapOrdersEndpoints();
app.MapCacheEndpoints();
app.MapJobEndpoints();

app.Run();

static async Task SeedData(AppDbContext db)
{
    if (await db.Customers.AnyAsync())
        return;

    var tenants = new[] { "tenant-a", "tenant-b" };

    foreach (var tenantId in tenants)
    {
        var customers = Enumerable.Range(1, 3).Select(i => new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Customer {i} ({tenantId})",
            Email = $"customer{i}@{tenantId}.com"
        }).ToList();

        var goods = new[]
        {
            new Good { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Laptop", Price = 999.99m, Category = "Electronics" },
            new Good { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Mouse", Price = 29.99m, Category = "Electronics" },
            new Good { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Desk", Price = 249.99m, Category = "Furniture" },
            new Good { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Chair", Price = 199.99m, Category = "Furniture" },
            new Good { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Notebook", Price = 4.99m, Category = "Stationery" },
        };

        db.Customers.AddRange(customers);
        db.Goods.AddRange(goods);

        // Create a sample order
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customers[0].Id,
            Status = OrderStatus.Confirmed
        };
        db.Orders.Add(order);

        db.OrderItems.AddRange(
            new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, GoodId = goods[0].Id, Quantity = 1, UnitPrice = goods[0].Price },
            new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, GoodId = goods[1].Id, Quantity = 2, UnitPrice = goods[1].Price }
        );
    }

    await db.SaveChangesAsync();
}

// Make the auto-generated Program class accessible for WebApplicationFactory
public partial class Program;
