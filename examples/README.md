# CachedQueries Demo

A minimal API demonstrating CachedQueries with PostgreSQL, Redis, multi-tenant isolation, and transaction-aware cache invalidation.

Compatible with .NET 8, .NET 9, and .NET 10.

## Quick Start

```bash
cd examples
docker compose up --build
```

Open Swagger UI: http://localhost:8080/swagger

## Integration Tests

73 integration tests against real PostgreSQL + Redis via Testcontainers:

```bash
cd examples
dotnet test Demo.Api.Tests
```

Tests cover: CRUD, auto-invalidation, custom tags, transaction-aware invalidation, multi-tenant isolation, concurrent access (thundering herd, readers during writes), Redis key management, and cache consistency.

## What's Demonstrated

### Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpContextAccessor();

builder.Services.AddCachedQueries(config =>
{
    config.DefaultOptions = new CachingOptions(TimeSpan.FromMinutes(30));
    config.AutoInvalidation = true;
    config.UseContextProvider<TenantCacheContextProvider>();
});

builder.Services.AddCacheInvalidation<AppDbContext>();

var app = builder.Build();
app.Services.UseCachedQueries();
```

### Automatic Cache Invalidation

All queries using `.Cacheable()` are automatically invalidated when `SaveChanges()` modifies related entities. No manual cache management needed.

```csharp
var customers = await db.Customers
    .Where(c => c.TenantId == tenantId)
    .Cacheable()
    .ToListAsync();
```

### Custom Tags

Tag cached queries for grouped invalidation:

```csharp
var goods = await db.Goods
    .Where(g => g.Category == category)
    .Cacheable(o => o
        .Expire(TimeSpan.FromMinutes(10))
        .WithTags("goods-catalog", $"category:{category}"))
    .ToListAsync();

// Later: invalidate all queries tagged with a specific category
await Cache.InvalidateByTagAsync($"category:Electronics");
```

### Transaction-Aware Invalidation

Cache invalidation is deferred until the transaction commits. If the transaction rolls back, cached data stays untouched:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync();

db.Orders.Add(order);
db.OrderItems.AddRange(items);
await db.SaveChangesAsync();  // cache invalidation is DEFERRED

await transaction.CommitAsync(); // NOW cache is invalidated
```

### Multi-Tenant Cache Isolation

Each tenant gets its own cache namespace via `X-Tenant-Id` header:

```bash
# Tenant A sees only their data
curl -H "X-Tenant-Id: tenant-a" http://localhost:8080/api/customers

# Tenant B has separate cache
curl -H "X-Tenant-Id: tenant-b" http://localhost:8080/api/customers

# Clearing tenant A's cache doesn't affect tenant B
curl -X POST -H "X-Tenant-Id: tenant-a" http://localhost:8080/api/customers/clear-cache
```

### Scalar & Boolean Caching

Count and existence checks are cached too:

```csharp
var count = await db.Goods.Cacheable().CountAsync();
var exists = await db.Customers.Cacheable().AnyAsync(c => c.Email == email);
```

## API Endpoints

### Customers
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/customers` | List customers (cached) |
| GET | `/api/customers/{id}` | Get by ID (cached) |
| GET | `/api/customers/exists?email=...` | Check existence (cached) |
| POST | `/api/customers` | Create (auto-invalidates cache) |
| POST | `/api/customers/clear-cache` | Clear tenant cache |

### Goods
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/goods` | List goods (cached) |
| GET | `/api/goods/by-category/{category}` | By category with custom tag |
| GET | `/api/goods/count` | Count (scalar cache) |
| GET | `/api/goods/{id}` | Get by ID (cached) |
| POST | `/api/goods` | Create (auto-invalidates) |
| POST | `/api/goods/invalidate-category/{category}` | Manual tag invalidation |

### Orders
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/orders` | List with includes (cached) |
| GET | `/api/orders/{id}` | Get by ID (cached) |
| POST | `/api/orders` | Create in transaction |
| PUT | `/api/orders/{id}/status` | Update status |
| POST | `/api/orders/invalidate` | Manual tag invalidation |

### Cache Management
| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/cache/clear-all` | Clear all cache |
| POST | `/api/cache/clear-tenant` | Clear current tenant cache |
| POST | `/api/cache/invalidate-entity/{type}` | Invalidate by entity type |

## Seed Data

The app seeds two tenants (`tenant-a`, `tenant-b`) each with:
- 3 customers
- 5 goods (Electronics, Furniture, Stationery)
- 1 sample order with 2 items
