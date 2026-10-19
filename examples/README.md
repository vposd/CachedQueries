# CachedQueries Demo

A minimal API demonstrating every CachedQueries feature — and its main pitfalls — with PostgreSQL, Redis,
multi-tenant isolation, transactions and a background service.

Compatible with .NET 8 and .NET 9.

## Quick Start

```bash
cd examples
docker compose up --build
```

This starts PostgreSQL, Redis and the API (configured with `Cache__Provider=Redis`).
Open Swagger UI: http://localhost:8080/swagger

To run the API locally with the in-memory provider instead, start only PostgreSQL (`docker compose up postgres`) and
run `dotnet run --project Demo.Api` (`appsettings.json` sets `Cache:Provider` to `Memory`). Set `Cache:Provider`
to `Redis` and `ConnectionStrings:Redis` to use a local Redis.

## Integration Tests

90 integration tests against real PostgreSQL + Redis via Testcontainers (Docker required):

```bash
cd examples
dotnet test Demo.Api.Tests
```

`FeatureShowcaseTests` has one test per scenario below. The other suites cover CRUD, auto-invalidation, custom tags,
transactions, multi-tenant isolation, concurrent access (thundering herd, readers during writes), Redis key management,
Redis Lua script behaviour (large tag sets, tag TTLs, sliding expiration), and cache consistency.

## Feature Map

| Feature                                        | Where                                                   |
|------------------------------------------------|---------------------------------------------------------|
| `.Cacheable()` + `ToListAsync`                 | `GET /api/customers`, `GET /api/goods`                  |
| `FirstOrDefaultAsync`                          | `GET /api/goods/{id}`, `GET /api/orders/{id}`           |
| `SingleOrDefaultAsync`                         | `GET /api/customers/by-email`                           |
| `CountAsync` / `AnyAsync`                      | `GET /api/goods/count`, `GET /api/customers/exists`     |
| `Expire()`                                     | most endpoints                                          |
| `SlidingExpiration()` + `IgnoreContext()`      | `GET /api/goods/categories` (global reference data)     |
| `SkipIf()`                                     | `GET /api/goods?fresh=true`                             |
| `WithKey()` + `Cache.InvalidateByKeyAsync`     | `GET /api/customers/{id}`, `POST /api/customers/{id}/invalidate` |
| `Cache.InvalidateByKeysAsync`                  | `POST /api/customers/invalidate`                        |
| `WithTags()` + `CacheTags.ForEntity<T>()`      | `GET /api/goods/by-category/{category}`, `GET /api/orders` |
| `Cache.InvalidateByTagAsync` / `...TagsAsync`  | `POST /api/goods/invalidate-category(-ies)`, `POST /api/orders/invalidate` |
| `Cache.InvalidateAsync<T>` / `ClearAllAsync`   | `POST /api/cache/invalidate-entity/{type}`, `POST /api/cache/clear-all` |
| Includes → dependencies                        | `GET /api/orders` (Order, Customer, OrderItem, Good)    |
| Subquery → dependency                          | `GET /api/customers/with-orders` (depends on Orders)    |
| Edit pattern (cached results are read-only)    | `PUT /api/customers/{id}`, `PUT /api/orders/{id}/status` |
| EF transaction                                 | `POST /api/orders`                                      |
| `TransactionScope`                             | `POST /api/orders/scoped`                               |
| `ExecuteUpdate` + manual invalidation          | `POST /api/goods/reprice`                               |
| Background service + explicit tenant invalidation | `POST /api/jobs/price-adjustment`, `Jobs/PriceAdjustmentJobs.cs` |
| Multi-tenant isolation (`ICacheContextProvider`) | `TenantCacheContextProvider.cs`, `X-Tenant-Id` header |
| Memory / Redis provider switch, multi-provider | `Program.cs`                                            |

## Scenarios

All requests below use `-H "X-Tenant-Id: tenant-a"` unless stated otherwise. The demo trusts this header for
simplicity; a real application must derive the tenant from authenticated data such as a claim (see `Tenancy.cs`).

### Automatic invalidation

```csharp
var customers = await db.Customers
    .Where(c => c.TenantId == tenantId)
    .Cacheable()
    .ToListAsync();
```

`POST /api/customers` calls `SaveChanges`, which invalidates every cached query depending on `Customer` for the
current tenant (and global entries).

Dependencies are detected from the whole query, including subqueries — `GET /api/customers/with-orders` depends on
`Order` too, so creating an order refreshes it:

```csharp
db.Customers
    .Where(c => c.TenantId == tenantId && db.Orders.Any(o => o.CustomerId == c.Id))
    .Cacheable()
    .ToListAsync();
```

### Editing data — cached results are read-only

Results served through the cache are loaded with `AsNoTracking()` and shared between requests. To change an entity,
load it without `.Cacheable()`:

```csharp
var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
customer.Name = req.Name;
await db.SaveChangesAsync();                       // invalidates auto-tracked Customer queries
await Cache.InvalidateByKeyAsync($"customer:{id}"); // WithKey() entries are your responsibility
```

### Custom keys and tags

`WithKey()` and `WithTags()` replace automatic tracking. Add the entity tag explicitly to keep `SaveChanges`
invalidation as well:

```csharp
db.Goods
    .Where(g => g.TenantId == tenantId && g.Category == category)
    .Cacheable(o => o
        .Expire(TimeSpan.FromMinutes(10))
        .WithTags("goods-catalog", $"category:{category}", CacheTags.ForEntity<Good>()))
    .ToListAsync();

await Cache.InvalidateByTagAsync($"category:{category}");
await Cache.InvalidateByTagsAsync(["category:Furniture", "category:Electronics"]);
```

### Global reference data

```csharp
db.Goods.Select(g => g.Category).Distinct().OrderBy(c => c)
    .Cacheable(o => o
        .IgnoreContext()                            // one entry shared by all tenants
        .SlidingExpiration(TimeSpan.FromMinutes(20)))
    .ToListAsync();
```

Global entries are invalidated by changes from any tenant.

### Bypassing the cache

`GET /api/goods?fresh=true` uses `.Cacheable(o => o.SkipIf(fresh))` — useful right after a write when
read-your-writes matters.

### Transactions

```csharp
await using var transaction = await db.Database.BeginTransactionAsync();
await db.SaveChangesAsync();     // invalidation DEFERRED
await transaction.CommitAsync(); // NOW cache is invalidated (rollback: nothing is invalidated)
```

`POST /api/orders/scoped` does the same with a `TransactionScope`: invalidation runs when the scope commits.

### Bulk updates (`ExecuteUpdate`)

`ExecuteUpdate`/`ExecuteDelete` bypass `SaveChanges`, so nothing is invalidated automatically:

```bash
# stale: the cached by-category list keeps the old prices
curl -X POST -H "X-Tenant-Id: tenant-a" -H "Content-Type: application/json" \
  -d '{"category":"Furniture","percent":10,"invalidateCache":false}' http://localhost:8080/api/goods/reprice

# correct: followed by Cache.InvalidateAsync<Good>()
curl -X POST -H "X-Tenant-Id: tenant-a" -H "Content-Type: application/json" \
  -d '{"category":"Furniture","percent":10}' http://localhost:8080/api/goods/reprice
```

### Background services

A background service has no HTTP request, so the context provider returns `null` and `SaveChanges` only invalidates
**global** entries. `PriceAdjustmentWorker` invalidates the tenant's entries explicitly:

```csharp
await db.SaveChangesAsync();
await invalidator.InvalidateAsync([typeof(Good)], job.TenantId);
```

```bash
curl -X POST -H "X-Tenant-Id: tenant-a" -H "Content-Type: application/json" \
  -d '{"category":"Electronics","percent":5}' http://localhost:8080/api/jobs/price-adjustment
curl http://localhost:8080/api/jobs/{jobId}
```

Pass `"explicitInvalidation": false` to see the tenant's cached goods stay stale.

### Multi-tenant isolation

```bash
curl -H "X-Tenant-Id: tenant-a" http://localhost:8080/api/customers   # tenant A's cache
curl -H "X-Tenant-Id: tenant-b" http://localhost:8080/api/customers   # tenant B's cache

# Invalidating as tenant A doesn't affect tenant B's entries
curl -X POST -H "X-Tenant-Id: tenant-a" http://localhost:8080/api/cache/invalidate-entity/Customer
```

## API Endpoints

### Customers
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/customers` | List customers (cached) |
| GET | `/api/customers/with-orders` | Customers having orders (subquery dependency) |
| GET | `/api/customers/by-email?email=...` | Single customer by email (`SingleOrDefault`) |
| GET | `/api/customers/exists?email=...` | Check existence (cached) |
| GET | `/api/customers/{id}` | Get by ID (custom key) |
| PUT | `/api/customers/{id}` | Update (auto-invalidation + key invalidation) |
| POST | `/api/customers` | Create (auto-invalidates cache) |
| POST | `/api/customers/{id}/invalidate` | Invalidate one custom key |
| POST | `/api/customers/invalidate` | Invalidate several custom keys (body: `["id", ...]`) |

### Goods
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/goods[?fresh=true]` | List goods (cached; `fresh` skips the cache) |
| GET | `/api/goods/categories` | Global categories (`IgnoreContext`, sliding) |
| GET | `/api/goods/by-category/{category}` | By category with custom + entity tags |
| GET | `/api/goods/count` | Count (scalar cache) |
| GET | `/api/goods/{id}` | Get by ID (cached) |
| POST | `/api/goods` | Create (auto-invalidates) |
| POST | `/api/goods/reprice` | Bulk `ExecuteUpdate` + manual invalidation |
| POST | `/api/goods/invalidate-category/{category}` | Manual tag invalidation |
| POST | `/api/goods/invalidate-categories` | Several tags (body: `["Furniture", ...]`) |

### Orders
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/orders` | List with includes (custom + entity tags) |
| GET | `/api/orders/{id}` | Get by ID (cached) |
| POST | `/api/orders` | Create in an EF transaction |
| POST | `/api/orders/scoped` | Create in a `TransactionScope` |
| PUT | `/api/orders/{id}/status` | Update status |
| POST | `/api/orders/invalidate` | Manual tag invalidation of the list |

### Jobs
| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/jobs/price-adjustment` | Enqueue a background price change for the current tenant |
| GET | `/api/jobs/{id}` | Job status |

### Cache Management
| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/cache/clear-all` | Clear all cache |
| POST | `/api/cache/invalidate-entity/{type}` | Invalidate by entity type |

## Seed Data

The app seeds two tenants (`tenant-a`, `tenant-b`) each with:
- 3 customers
- 5 goods (Electronics, Furniture, Stationery)
- 1 sample order with 2 items
