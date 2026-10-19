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

        // GET /api/goods[?fresh=true] — cached with auto-invalidation by entity type.
        // SkipIf: ?fresh=true bypasses the cache and reads the database (e.g. right after a write,
        // when read-your-writes matters more than speed).
        group.MapGet("/", async (AppDbContext db, HttpContext http, bool fresh = false) =>
        {
            var tenantId = http.GetTenantId();
            var goods = await db.Goods
                .Where(g => g.TenantId == tenantId)
                .OrderBy(g => g.Name)
                .Cacheable(o => o.SkipIf(fresh))
                .ToListAsync();

            return Results.Ok(goods);
        });

        // GET /api/goods/categories — global reference data shared by all tenants.
        // IgnoreContext(): one cache entry for everybody instead of one per tenant.
        // SlidingExpiration(): stays cached while it keeps being read.
        // Global entries are still invalidated by any tenant's Good changes.
        group.MapGet("/categories", async (AppDbContext db) =>
        {
            var categories = await db.Goods
                .Select(g => g.Category)
                .Distinct()
                .OrderBy(c => c)
                .Cacheable(o => o
                    .IgnoreContext()
                    .SlidingExpiration(TimeSpan.FromMinutes(20)))
                .ToListAsync();

            return Results.Ok(categories);
        });

        // GET /api/goods/by-category/{category} — cached with custom tags.
        // WithTags() replaces automatic tracking, so the Good entity tag is added explicitly:
        // the entry is invalidated both by the category tags and by SaveChanges on Good.
        group.MapGet("/by-category/{category}", async (string category, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var goods = await db.Goods
                .Where(g => g.TenantId == tenantId && g.Category == category)
                .Cacheable(o => o
                    .Expire(TimeSpan.FromMinutes(10))
                    .WithTags("goods-catalog", CategoryTag(category), CacheTags.ForEntity<Good>()))
                .ToListAsync();

            return Results.Ok(goods);
        });

        // GET /api/goods/count — scalar caching demo
        group.MapGet("/count", async (AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var count = await db.Goods
                .Where(g => g.TenantId == tenantId)
                .Cacheable(o => o.Expire(TimeSpan.FromMinutes(5)))
                .CountAsync();

            return Results.Ok(new { count });
        });

        // GET /api/goods/{id}
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var good = await db.Goods
                .Where(g => g.Id == id && g.TenantId == tenantId)
                .Cacheable(o => o.Expire(TimeSpan.FromMinutes(15)))
                .FirstOrDefaultAsync();

            return good is null ? Results.NotFound() : Results.Ok(good);
        });

        // POST /api/goods — auto-invalidates Good entity cache on save
        group.MapPost("/", async (CreateGoodRequest req, AppDbContext db, HttpContext http) =>
        {
            var good = new Good
            {
                Id = Guid.NewGuid(),
                TenantId = http.GetTenantId(),
                Name = req.Name,
                Price = req.Price,
                Category = req.Category
            };

            db.Goods.Add(good);
            await db.SaveChangesAsync(); // ← auto-invalidates cached Good queries

            return Results.Created($"/api/goods/{good.Id}", good);
        });

        // POST /api/goods/reprice — bulk update with ExecuteUpdate.
        // ExecuteUpdate/ExecuteDelete bypass SaveChanges, so nothing is invalidated automatically:
        // invalidate the affected entity type yourself right after the statement.
        group.MapPost("/reprice", async (RepriceRequest req, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var factor = 1 + req.Percent / 100m;

            var updated = await db.Goods
                .Where(g => g.TenantId == tenantId && g.Category == req.Category)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Price, g => Math.Round(g.Price * factor, 2)));

            if (req.InvalidateCache)
            {
                await Cache.InvalidateAsync<Good>(); // ← required after bulk updates
            }

            return Results.Ok(new { updated });
        });

        // POST /api/goods/invalidate-category/{category} — manual tag invalidation
        group.MapPost("/invalidate-category/{category}", async (string category) =>
        {
            await Cache.InvalidateByTagAsync(CategoryTag(category));
            return Results.Ok(new { message = $"Cache invalidated for category '{category}'" });
        });

        // POST /api/goods/invalidate-categories — several tags in one call
        group.MapPost("/invalidate-categories", async (string[] categories) =>
        {
            await Cache.InvalidateByTagsAsync(categories.Select(CategoryTag));
            return Results.Ok(new { message = $"Cache invalidated for {categories.Length} categories" });
        });
    }

    private static string CategoryTag(string category)
    {
        return $"category:{category}";
    }

    public record CreateGoodRequest(string Name, decimal Price, string Category);

    // InvalidateCache = false shows the stale result you get when a bulk update is not followed by invalidation.
    public record RepriceRequest(string Category, decimal Percent, bool InvalidateCache = true);
}
