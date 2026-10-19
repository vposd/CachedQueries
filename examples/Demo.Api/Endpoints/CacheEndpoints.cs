using CachedQueries;
using CachedQueries.Extensions;
using Demo.Api.Entities;

namespace Demo.Api.Endpoints;

public static class CacheEndpoints
{
    public static void MapCacheEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cache").WithTags("Cache");

        // POST /api/cache/clear-all — everything, all tenants, all providers
        group.MapPost("/clear-all", async () =>
        {
            await Cache.ClearAllAsync();
            return Results.Ok(new { message = "All cache cleared" });
        });

        // POST /api/cache/invalidate-entity/{entityType} — current tenant's + global entries of an entity type
        group.MapPost("/invalidate-entity/{entityType}", async (string entityType) =>
        {
            var type = entityType.ToLowerInvariant() switch
            {
                "customer" or "customers" => typeof(Customer),
                "good" or "goods" => typeof(Good),
                "order" or "orders" => typeof(Order),
                "orderitem" or "orderitems" => typeof(OrderItem),
                _ => null
            };

            if (type is null)
            {
                return Results.BadRequest(new { error = $"Unknown entity type: {entityType}" });
            }

            await CacheExtensions.InvalidateAsync([type]);
            return Results.Ok(new { message = $"Cache invalidated for {entityType}", tag = CacheTags.ForEntity(type) });
        });
    }
}
