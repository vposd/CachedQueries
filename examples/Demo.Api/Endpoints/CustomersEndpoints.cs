using CachedQueries;
using CachedQueries.Extensions;
using Demo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api.Endpoints;

public static class CustomersEndpoints
{
    public static void MapCustomersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers");

        // GET /api/customers — cached with default options (auto entity-type invalidation)
        group.MapGet("/", async (AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var customers = await db.Customers
                .Where(c => c.TenantId == tenantId)
                .OrderBy(c => c.Name)
                .Cacheable()
                .ToListAsync();

            return Results.Ok(customers);
        });

        // GET /api/customers/with-orders — dependency through a subquery.
        // The query reads Orders inside Any(), so CachedQueries tracks it under both Customer and Order:
        // creating an order for a customer invalidates this list automatically.
        group.MapGet("/with-orders", async (AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var customers = await db.Customers
                .Where(c => c.TenantId == tenantId && db.Orders.Any(o => o.CustomerId == c.Id))
                .OrderBy(c => c.Name)
                .Cacheable()
                .ToListAsync();

            return Results.Ok(customers);
        });

        // GET /api/customers/by-email?email=... — SingleOrDefault (emails are unique per tenant).
        // A null result (no such customer) is not cached.
        group.MapGet("/by-email", async (string email, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var customer = await db.Customers
                .Where(c => c.TenantId == tenantId)
                .Cacheable()
                .SingleOrDefaultAsync(c => c.Email == email);

            return customer is null ? Results.NotFound() : Results.Ok(customer);
        });

        // GET /api/customers/exists?email=... — cached boolean check
        group.MapGet("/exists", async (string email, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var exists = await db.Customers
                .Where(c => c.TenantId == tenantId)
                .Cacheable(o => o.Expire(TimeSpan.FromMinutes(5)))
                .AnyAsync(c => c.Email == email);

            return Results.Ok(new { exists });
        });

        // GET /api/customers/{id} — cached with custom key for direct key-based invalidation.
        // Entries with a custom key are NOT invalidated by SaveChanges: whoever changes the customer
        // must invalidate the key (see PUT below).
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var customer = await db.Customers
                .Where(c => c.Id == id && c.TenantId == tenantId)
                .Cacheable(o => o
                    .WithKey(CustomerKey(id))
                    .Expire(TimeSpan.FromMinutes(10)))
                .FirstOrDefaultAsync();

            return customer is null ? Results.NotFound() : Results.Ok(customer);
        });

        // PUT /api/customers/{id} — the "edit" pattern.
        // Cached results are read-only (loaded with AsNoTracking and shared between requests), so the entity
        // to modify is loaded WITHOUT .Cacheable(). SaveChanges invalidates auto-tracked queries (lists, exists, ...);
        // the custom-key entry from GET /{id} has to be invalidated explicitly.
        group.MapPut("/{id:guid}", async (Guid id, UpdateCustomerRequest req, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (customer is null)
            {
                return Results.NotFound();
            }

            customer.Name = req.Name;
            customer.Email = req.Email;
            await db.SaveChangesAsync(); // ← auto-invalidates Customer queries of this tenant (+ global ones)
            await Cache.InvalidateByKeyAsync(CustomerKey(id)); // ← custom-key entry is our responsibility

            return Results.Ok(customer);
        });

        // POST /api/customers/{id}/invalidate — invalidate a single customer's cache by key
        group.MapPost("/{id:guid}/invalidate", async (Guid id) =>
        {
            await Cache.InvalidateByKeyAsync(CustomerKey(id));
            return Results.Ok(new { message = $"Cache invalidated for customer {id}" });
        });

        // POST /api/customers/invalidate — invalidate several customers by key in one call
        group.MapPost("/invalidate", async (Guid[] ids) =>
        {
            await Cache.InvalidateByKeysAsync(ids.Select(CustomerKey));
            return Results.Ok(new { message = $"Cache invalidated for {ids.Length} customers" });
        });

        // POST /api/customers
        group.MapPost("/", async (CreateCustomerRequest req, AppDbContext db, HttpContext http) =>
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                TenantId = http.GetTenantId(),
                Name = req.Name,
                Email = req.Email
            };

            db.Customers.Add(customer);
            await db.SaveChangesAsync(); // ← auto-invalidates cached Customer queries

            return Results.Created($"/api/customers/{customer.Id}", customer);
        });
    }

    private static string CustomerKey(Guid id)
    {
        return $"customer:{id}";
    }

    public record CreateCustomerRequest(string Name, string? Email);

    public record UpdateCustomerRequest(string Name, string? Email);
}
