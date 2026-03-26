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
            var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";

            var customers = await db.Customers
                .Where(c => c.TenantId == tenantId)
                .OrderBy(c => c.Name)
                .Cacheable()
                .ToListAsync();

            return Results.Ok(customers);
        });

        // GET /api/customers/{id} — cached with custom key for direct key-based invalidation
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var customer = await db.Customers
                .Where(c => c.Id == id)
                .Cacheable(o => o
                    .WithKey($"customer:{id}")
                    .Expire(TimeSpan.FromMinutes(10)))
                .FirstOrDefaultAsync();

            return customer is null ? Results.NotFound() : Results.Ok(customer);
        });

        // POST /api/customers/{id}/invalidate — invalidate a single customer's cache by key
        group.MapPost("/{id:guid}/invalidate", async (Guid id) =>
        {
            await Cache.InvalidateByKeyAsync($"customer:{id}");
            return Results.Ok(new { message = $"Cache invalidated for customer {id}" });
        });

        // GET /api/customers/exists?email=... — cached boolean check
        group.MapGet("/exists", async (string email, AppDbContext db) =>
        {
            var exists = await db.Customers
                .Cacheable(o => o.Expire(TimeSpan.FromMinutes(5)))
                .AnyAsync(c => c.Email == email);

            return Results.Ok(new { exists });
        });

        // POST /api/customers
        group.MapPost("/", async (CreateCustomerRequest req, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = req.Name,
                Email = req.Email
            };

            db.Customers.Add(customer);
            await db.SaveChangesAsync(); // ← auto-invalidates cached Customer queries

            return Results.Created($"/api/customers/{customer.Id}", customer);
        });

    }

    public record CreateCustomerRequest(string Name, string? Email);
}
