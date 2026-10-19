# CachedQueries

[![NuGet](https://img.shields.io/nuget/v/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![CI](https://github.com/vposd/CachedQueries/actions/workflows/ci.yml/badge.svg)](https://github.com/vposd/CachedQueries/actions/workflows/ci.yml)
[![Coverage Status](https://coveralls.io/repos/github/vposd/CachedQueries/badge.svg?branch=main)](https://coveralls.io/github/vposd/CachedQueries?branch=main)
[![License](https://img.shields.io/:license-MIT-blue.svg)](https://vposd.mit-license.org/)

A .NET library for seamless caching of Entity Framework Core queries. Cache `IQueryable` results directly within EF with
automatic invalidation on `SaveChanges`, transaction-aware invalidation, multi-context isolation, and pluggable cache
providers.

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
builder.Services.AddCacheInvalidation<AppDbContext>(); // must come after AddDbContext

var app = builder.Build();
app.Services.UseCachedQueries();
app.Run();
```

> Until `UseCachedQueries()` runs, `.Cacheable()` queries execute without caching.

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
    .Cacheable(o => o.Expire(TimeSpan.FromMinutes(10)))
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

| Method                    | Description           |
|---------------------------|-----------------------|
| `.ToListAsync()`          | Cache as list         |
| `.FirstOrDefaultAsync()`  | Cache first item      |
| `.SingleOrDefaultAsync()` | Cache single item     |
| `.CountAsync()`           | Cache count           |
| `.AnyAsync()`             | Cache existence check |

Configure per-query behavior with the fluent builder:

```csharp
.Cacheable(o => o
    .Expire(TimeSpan.FromMinutes(5))             // Absolute expiration
    .SlidingExpiration(TimeSpan.FromMinutes(5))  // Or sliding
    .WithKey("my-custom-key")                    // Custom cache key (manual invalidation only)
    .WithTags("orders", "reports")               // Tags for grouped invalidation (see below)
    .IgnoreContext()                             // Share the entry across contexts (e.g. tenants)
    .SkipIf(bypassCache)                         // Conditional skip
    .UseTarget(CacheTarget.Collection))          // Override provider selection
```

### How results are cached

- **Results are read-only.** Queries served through the cache run with `AsNoTracking()` (unless the query already uses
  `AsNoTrackingWithIdentityResolution()`), because the same instances can be returned to many callers — with the
  in-memory provider, literally the same objects. To modify an entity, load it without `.Cacheable()`.
- **Stampede protection.** Concurrent cache misses for the same key are coalesced: one caller queries the database, the
  others wait for its result.
- **`null` is not cached.** `FirstOrDefaultAsync`/`SingleOrDefaultAsync` returning `null` hit the database every time.
- **Cache keys** are a SHA-256 hash of the generated SQL (with parameter values) plus a rendering of the expression tree
  with captured variable values, so `Where(o => o.Id == id)` gets a different key for every `id` on every provider,
  including non-relational ones (InMemory, Cosmos).
- `SkipIf(true)` or an unconfigured library executes the query normally, with tracking.

## Cache Invalidation

### Automatic (default)

Cache is invalidated automatically on `SaveChanges()`. The library detects which entity types were modified and
invalidates all cached queries that depend on them.

```csharp
db.Orders.Add(newOrder);
await db.SaveChangesAsync(); // All cached Order queries are invalidated
```

A query depends on every entity type it touches: the root set, `Include`d navigations, navigations used in filters or
projections, and other sets used in joins or subqueries. On the write side, changed types are read from EF metadata, so:

- changing a derived entity (`Dog`) also invalidates queries on its base type (`Animal`);
- changing an owned entity invalidates its owner;
- adding/removing a many-to-many link invalidates both sides;
- lazy-loading proxies are handled.

### Transaction-Aware

Inside a transaction, invalidation is deferred until commit. Rollback discards pending invalidations.

```csharp
await using var tx = await db.Database.BeginTransactionAsync();

db.Orders.Add(order);
await db.SaveChangesAsync();  // Invalidation DEFERRED

await tx.CommitAsync();       // Invalidation fires NOW
// Or: await tx.RollbackAsync();  // No invalidation
```

Ambient `System.Transactions.TransactionScope`s are supported too: invalidation runs when the scope commits.

### Manual

```csharp
using CachedQueries.Extensions;

await Cache.InvalidateByKeyAsync("my-key");            // By exact cache key
await Cache.InvalidateByKeysAsync(["key1", "key2"]);   // By multiple keys
await Cache.InvalidateAsync<Order>();                  // By entity type
await Cache.InvalidateByTagAsync("reports");           // By single tag
await Cache.InvalidateByTagsAsync(["orders", "reports"]); // By multiple tags
await Cache.ClearAllAsync();                           // Everything
```

Key-based invalidation is useful when you cache with a custom key via `WithKey()`:

```csharp
// Cache with a custom key
var order = await db.Orders
    .Where(o => o.Id == id)
    .Cacheable(o => o.WithKey($"order:{id}"))
    .FirstOrDefaultAsync();

// Later, invalidate by that same key — no tags or entity tracking needed
await Cache.InvalidateByKeyAsync($"order:{id}");
```

### Tags vs. automatic invalidation

`WithKey()` and `WithTags()` **replace** automatic entity tracking: such entries are only invalidated manually (or by
expiration). To keep `SaveChanges`-driven invalidation for an entity type while also tagging an entry, add the entity's
tag explicitly with `CacheTags.ForEntity<T>()`:

```csharp
var report = await db.Orders
    .Cacheable(o => o.WithTags("reports", CacheTags.ForEntity<Order>()))
    .ToListAsync();

await Cache.InvalidateByTagAsync("reports");                     // manual
// ...and SaveChanges on Order invalidates it automatically, too.

await Cache.InvalidateByTagAsync(CacheTags.ForEntity<Order>());  // same as Cache.InvalidateAsync<Order>()
```

## Redis Support

```csharp
// Automatic setup (recommended): one shared connection, atomic tag operations
builder.Services.AddCachedQueriesWithRedis("localhost:6379");

// Or with existing IDistributedCache — also register IConnectionMultiplexer for atomic tag operations
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost:6379"));
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost:6379");
builder.Services.AddCachedQueriesWithRedis();
```

- Values are serialized with System.Text.Json (camelCase, `ReferenceHandler.IgnoreCycles`): cyclic back-references
  (e.g. `Order.Items[i].Order`) come back as `null`. Types must be deserializable by System.Text.Json; failures are
  logged and treated as cache misses.
- Sliding expiration is honoured: a hit refreshes the entry and its tag sets.
- **Redis Cluster is not supported** — the Lua scripts touch keys in different hash slots. Standalone,
  primary/replica and Sentinel deployments work.
- Without an `IConnectionMultiplexer` the provider runs in a non-atomic fallback mode intended for development.

## Multi-Context Caching

For example, implement `ICacheContextProvider` to isolate cache per tenant:

```csharp
public class TenantCacheContextProvider(IHttpContextAccessor http) : ICacheContextProvider
{
    // Use an authenticated source (e.g. a claim), not raw client input
    public string? GetContextKey()
        => http.HttpContext?.User.FindFirst("tenant_id")?.Value;
}
```

Register it:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddCachedQueries(config =>
    config.UseContextProvider<TenantCacheContextProvider>());
```

Cache keys are automatically prefixed: `cq:tenant-a:abc123`. Invalidation removes the current context's entries plus
global (`IgnoreContext()`) entries; other contexts are untouched.

Things to know:

- The provider is resolved from a **new DI scope** on each cache operation, not from the request scope. Read ambient
  state (`IHttpContextAccessor`, claims, an `AsyncLocal`), not values that middleware stored in another scoped service.
- Code without an ambient context (background services, message handlers) has a `null` context key, so its
  `SaveChanges` only invalidates **global** entries. Invalidate the tenant explicitly there:

  ```csharp
  var invalidator = serviceProvider.GetRequiredService<ICacheInvalidator>();
  await invalidator.InvalidateAsync([typeof(Order)], tenantId);
  ```

## Configuration

```csharp
builder.Services.AddCachedQueries(config =>
{
    config.DefaultOptions = new CachingOptions(TimeSpan.FromHours(1)); // default expiration for .Cacheable()
    config.AutoInvalidation = true;  // default; false = no SaveChanges interceptors, manual invalidation only
    config.CachePrefix = "cq";       // default; namespace for all keys and tags
});
```

`DefaultOptions` supplies the expiration (absolute or sliding) for `.Cacheable()` and `.Cacheable(o => ...)`.
Options passed explicitly with `.Cacheable(new CachingOptions(...))` are used as-is.
Logging uses the standard `ILogger` categories under `CachedQueries`; configure their levels as usual.

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

| Target       | Auto-selected for                   | Description                        |
|--------------|-------------------------------------|------------------------------------|
| `Single`     | `FirstOrDefault`, `SingleOrDefault` | Individual entities                |
| `Collection` | `ToList`                            | Lists                              |
| `Scalar`     | `Count`, `Any`                      | Aggregation results                |
| `Auto`       | -                                   | Automatically determined (default) |

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

A provider must be thread-safe, return `default` on a miss, and associate each entry with every tag in
`options.Tags` so that `InvalidateByTagsAsync` removes it. See the XML docs on `ICacheProvider` for the full contract.

## Consistency & Limitations

CachedQueries is a look-aside cache with invalidation. It shortens, but does not eliminate, windows in which stale data
can be served:

- **Concurrent read during a write.** If a query misses the cache, reads the database, and a concurrent `SaveChanges`
  invalidates before that query stores its result, the (now stale) result is stored and served until it expires or the
  next invalidation. Keep expirations reasonable for data that must be fresh, or bypass the cache (`SkipIf`) where
  read-your-writes matters.
- **Only `SaveChanges` is observed.** `ExecuteUpdate`/`ExecuteDelete`, raw SQL, stored procedures, and other
  applications writing to the database do not invalidate anything — call `Cache.InvalidateAsync<T>()` yourself.
- **External transactions.** If a context joins a transaction via `Database.UseTransaction(...)` and the transaction is
  committed outside EF (or through another context), no commit is observed for it — invalidate manually after commit.
- **String includes** (`Include("Items")`) are not detected as dependencies; use lambda includes or tags.
- **Process-wide configuration.** `UseCachedQueries()` configures a static accessor: one configuration per process.
- **Memory provider** is per process. Use Redis (or another distributed provider) when running several instances.
- Cache invalidation failures are logged and never fail `SaveChanges`.

## Legacy API

The older extension methods are still supported:

```csharp
await db.Orders.ToListCachedAsync(ct);
await db.Products.FirstOrDefaultCachedAsync(p => p.Id == id, ct);
await db.Orders.CountCachedAsync(ct);
await db.Orders.AnyCachedAsync(o => o.Status == OrderStatus.Pending, ct);
```

## Demo Project

See [`examples/`](examples/) for a full working demo covering every feature (with a feature map): Docker Compose,
PostgreSQL, Redis, multi-context isolation, transactions, bulk updates, background services,
and integration tests (they start PostgreSQL and Redis with Testcontainers, so Docker is required).

```bash
cd examples
docker compose up --build        # Run the API
dotnet test Demo.Api.Tests       # Run integration tests
```

## Requirements

- .NET 8.0 or 9.0
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
