# CachedQueries

[![NuGet](https://img.shields.io/nuget/v/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![CI](https://github.com/valery-pozdnyakov/CachedQueries/actions/workflows/ci.yml/badge.svg)](https://github.com/valery-pozdnyakov/CachedQueries/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET library for seamless caching of Entity Framework Core queries. Cache `IQueryable` results directly within EF with automatic invalidation on `SaveChanges`, transaction-aware invalidation, multi-tenant isolation, and pluggable cache providers.

## Installation

```bash
dotnet add package CachedQueries
```

For Redis support:
```bash
dotnet add package CachedQueries.Redis
```

## Quick Start

### 1. Configure Services

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddCachedQueries();
builder.Services.AddCacheInvalidation<AppDbContext>();

var app = builder.Build();
app.Services.UseCachedQueries();
app.Run();
```

### 2. Cache Queries

```csharp
using CachedQueries.Extensions;

// Cache a collection (default 30 min expiration)
var orders = await db.Orders
    .Include(o => o.Items)
    .Cacheable()
    .ToListAsync();

// Cache with custom options
var goods = await db.Goods
    .Where(g => g.Category == "Electronics")
    .Cacheable(o => o
        .Expire(TimeSpan.FromMinutes(10))
        .WithTags("catalog", "electronics"))
    .ToListAsync();

// Cache single item
var customer = await db.Customers
    .Where(c => c.Id == id)
    .Cacheable()
    .FirstOrDefaultAsync();

// Cache scalar results
var count = await db.Orders.Cacheable().CountAsync();
var exists = await db.Customers.Cacheable().AnyAsync(c => c.Email == email);
```

That's it. Cache is automatically invalidated when `SaveChanges()` modifies related entities.

## Fluent API

The `.Cacheable()` extension returns a `CacheableQuery<T>` that supports all common terminal methods:

| Method | Description |
|--------|-------------|
| `.ToListAsync()` | Cache as list |
| `.FirstOrDefaultAsync()` | Cache first item |
| `.SingleOrDefaultAsync()` | Cache single item |
| `.CountAsync()` | Cache count |
| `.AnyAsync()` | Cache existence check |

Configure per-query behavior with the fluent builder:

```csharp
.Cacheable(o => o
    .Expire(TimeSpan.FromMinutes(5))      // Absolute expiration
    .SlidingExpiration(TimeSpan.FromMinutes(5))  // Or sliding
    .WithKey("my-custom-key")             // Custom cache key
    .WithTags("orders", "reports")        // Tags for grouped invalidation
    .SkipIf(bypassCache)                  // Conditional skip
    .UseTarget(CacheTarget.Collection))   // Override provider selection
```

## Cache Invalidation

### Automatic (default)

Cache is invalidated automatically on `SaveChanges()`. The library detects which entity types were modified and invalidates all cached queries that depend on them.

```csharp
db.Orders.Add(newOrder);
await db.SaveChangesAsync(); // All cached Order queries are invalidated
```

### Transaction-Aware

Inside a transaction, invalidation is deferred until commit. Rollback discards pending invalidations.

```csharp
await using var tx = await db.Database.BeginTransactionAsync();

db.Orders.Add(order);
await db.SaveChangesAsync();  // Invalidation DEFERRED

await tx.CommitAsync();       // Invalidation fires NOW
// Or: await tx.RollbackAsync();  // No invalidation
```

### Manual

```csharp
using CachedQueries.Extensions;

await Cache.InvalidateAsync<Order>();                  // By entity type
await Cache.InvalidateByTagAsync("reports");           // By single tag
await Cache.InvalidateByTagsAsync(["orders", "reports"]); // By multiple tags
await Cache.ClearContextAsync();                       // Current tenant only
await Cache.ClearAllAsync();                           // Everything
```

## Redis Support

```csharp
// Automatic setup (recommended)
builder.Services.AddCachedQueriesWithRedis("localhost:6379");

// Or with existing IDistributedCache
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost:6379");
builder.Services.AddCachedQueriesWithRedis();
```

## Multi-Tenant Caching

Implement `ICacheContextProvider` to isolate cache per tenant:

```csharp
public class TenantCacheContextProvider(IHttpContextAccessor http) : ICacheContextProvider
{
    public string? GetContextKey()
        => http.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
}
```

Register it:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddCachedQueries(config =>
    config.UseContextProvider<TenantCacheContextProvider>());
```

Cache keys are automatically prefixed: `tenant-a:CQ:abc123`.

## Configuration

```csharp
builder.Services.AddCachedQueries(config =>
{
    config.DefaultOptions = new CachingOptions(TimeSpan.FromHours(1));
    config.AutoInvalidation = true;  // default
    config.EnableLogging = true;     // default
});
```

## Multi-Provider Setup

Use different cache backends for different query types:

```csharp
builder.Services.AddCachedQueries(config => config
    .UseSingleItemProvider<RedisCacheProvider>()    // FirstOrDefault, SingleOrDefault
    .UseCollectionProvider<MongoCacheProvider>()    // ToList
    .UseScalarProvider<RedisCacheProvider>());      // Count, Any

// Or same provider for all
builder.Services.AddCachedQueries(config => config
    .UseProvider<RedisCacheProvider>());
```

### Cache Targets

| Target | Auto-selected for | Description |
|--------|-------------------|-------------|
| `Single` | `FirstOrDefault`, `SingleOrDefault` | Individual entities |
| `Collection` | `ToList` | Lists and arrays |
| `Scalar` | `Count`, `Any` | Aggregation results |
| `Auto` | - | Automatically determined (default) |

## Custom Cache Provider

```csharp
public class MyCacheProvider : ICacheProvider
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) { ... }
    public Task SetAsync<T>(string key, T value, CachingOptions options, CancellationToken ct = default) { ... }
    public Task RemoveAsync(string key, CancellationToken ct = default) { ... }
    public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default) { ... }
    public Task ClearAsync(CancellationToken ct = default) { ... }
}

builder.Services.AddCachedQueries<MyCacheProvider>();
```

## Legacy API

The older extension methods are still supported:

```csharp
await db.Orders.ToListCachedAsync(ct);
await db.Products.FirstOrDefaultCachedAsync(p => p.Id == id, ct);
await db.Orders.CountCachedAsync(ct);
await db.Orders.AnyCachedAsync(o => o.Status == OrderStatus.Pending, ct);
```

## Demo Project

See [`examples/`](examples/) for a full working demo with Docker Compose, PostgreSQL, Redis, multi-tenant isolation, and 73 integration tests.

```bash
cd examples
docker compose up --build        # Run the API
dotnet test Demo.Api.Tests       # Run integration tests
```

## Requirements

- .NET 8.0, 9.0, or 10.0
- Entity Framework Core 8.0+

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
