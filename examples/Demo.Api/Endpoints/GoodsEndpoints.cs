using CachedQueries;
using CachedQueries.Extensions;
using Demo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api.Endpoints;

public static class GoodsEndpoints
{
    public static void MapGoodsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/goods").WithTags("Goods");

        // GET /api/goods — cached with auto-invalidation by entity type
        group.MapGet("/", async (AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";

            var goods = await db.Goods
                .Where(g => g.TenantId == tenantId)
                .OrderBy(g => g.Name)
                .Cacheable()
                .ToListAsync();

            return Results.Ok(goods);
        });

        // GET /api/goods/by-category/{category} — cached with custom tag
        group.MapGet("/by-category/{category}", async (string category, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";

            var goods = await db.Goods
                .Where(g => g.TenantId == tenantId && g.Category == category)
                .Cacheable(o => o
                    .Expire(TimeSpan.FromMinutes(10))
                    .WithTags("goods-catalog", $"category:{category}"))
                .ToListAsync();

            return Results.Ok(goods);
        });

        // GET /api/goods/count — scalar caching demo
        group.MapGet("/count", async (AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";

            var count = await db.Goods
                .Where(g => g.TenantId == tenantId)
                .Cacheable(o => o.Expire(TimeSpan.FromMinutes(5)))
                .CountAsync();

            return Results.Ok(new { count });
        });

        // GET /api/goods/{id}
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var good = await db.Goods
                .Where(g => g.Id == id)
                .Cacheable(o => o.Expire(TimeSpan.FromMinutes(15)))
                .FirstOrDefaultAsync();

            return good is null ? Results.NotFound() : Results.Ok(good);
        });

        // POST /api/goods — auto-invalidates Good entity cache on save
        group.MapPost("/", async (CreateGoodRequest req, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";

            var good = new Good
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = req.Name,
                Price = req.Price,
                Category = req.Category
            };

            db.Goods.Add(good);
            await db.SaveChangesAsync(); // ← auto-invalidates cached Good queries

            return Results.Created($"/api/goods/{good.Id}", good);
        });

        // POST /api/goods/invalidate-category/{category} — manual tag invalidation
        group.MapPost("/invalidate-category/{category}", async (string category) =>
        {
            await Cache.InvalidateByTagAsync($"category:{category}");
            return Results.Ok(new { message = $"Cache invalidated for category '{category}'" });
        });
    }

    public record CreateGoodRequest(string Name, decimal Price, string Category);
}
