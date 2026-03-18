using CachedQueries;
using CachedQueries.Extensions;
using Demo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api.Endpoints;

public static class OrdersEndpoints
{
    public static void MapOrdersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        // GET /api/orders — cached with includes (auto-detects Order + Customer + OrderItem + Good)
        group.MapGet("/", async (AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";

            var orders = await db.Orders
                .Where(o => o.TenantId == tenantId)
                .Include(o => o.Customer)
                .Include(o => o.Items).ThenInclude(i => i.Good)
                .OrderByDescending(o => o.CreatedAt)
                .Cacheable(o => o
                    .Expire(TimeSpan.FromMinutes(5))
                    .WithTags("orders-list"))
                .ToListAsync();

            return Results.Ok(orders.Select(o => new
            {
                o.Id,
                o.Status,
                o.CreatedAt,
                Customer = o.Customer.Name,
                Items = o.Items.Select(i => new
                {
                    Good = i.Good.Name,
                    i.Quantity,
                    i.UnitPrice,
                    Total = i.Quantity * i.UnitPrice
                }),
                Total = o.Items.Sum(i => i.Quantity * i.UnitPrice)
            }));
        });

        // GET /api/orders/{id}
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var order = await db.Orders
                .Include(o => o.Customer)
                .Include(o => o.Items).ThenInclude(i => i.Good)
                .Where(o => o.Id == id)
                .Cacheable(o => o.Expire(TimeSpan.FromMinutes(10)))
                .FirstOrDefaultAsync();

            return order is null ? Results.NotFound() : Results.Ok(order);
        });

        // POST /api/orders — creates order inside a transaction (transaction-aware invalidation)
        group.MapPost("/", async (CreateOrderRequest req, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";

            await using var transaction = await db.Database.BeginTransactionAsync();

            try
            {
                var customer = await db.Customers.FindAsync(req.CustomerId);
                if (customer is null)
                    return Results.BadRequest(new { error = "Customer not found" });

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CustomerId = req.CustomerId
                };
                db.Orders.Add(order);

                foreach (var item in req.Items)
                {
                    var good = await db.Goods.FindAsync(item.GoodId);
                    if (good is null)
                        return Results.BadRequest(new { error = $"Good {item.GoodId} not found" });

                    db.OrderItems.Add(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        GoodId = item.GoodId,
                        Quantity = item.Quantity,
                        UnitPrice = good.Price
                    });
                }

                // SaveChanges inside transaction — cache invalidation is DEFERRED until commit
                await db.SaveChangesAsync();

                // Commit triggers cache invalidation for Order, OrderItem, etc.
                await transaction.CommitAsync();

                return Results.Created($"/api/orders/{order.Id}", new { order.Id });
            }
            catch
            {
                // Rollback discards pending cache invalidations — cache stays consistent
                await transaction.RollbackAsync();
                throw;
            }
        });

        // PUT /api/orders/{id}/status — update status, auto-invalidates
        group.MapPut("/{id:guid}/status", async (Guid id, UpdateStatusRequest req, AppDbContext db) =>
        {
            var order = await db.Orders.FindAsync(id);
            if (order is null) return Results.NotFound();

            order.Status = req.Status;
            await db.SaveChangesAsync(); // ← auto-invalidates Order cache

            return Results.Ok(new { order.Id, order.Status });
        });

        // POST /api/orders/invalidate — manual tag-based invalidation
        group.MapPost("/invalidate", async () =>
        {
            await Cache.InvalidateByTagAsync("orders-list");
            return Results.Ok(new { message = "Orders list cache invalidated" });
        });
    }

    public record CreateOrderRequest(Guid CustomerId, List<OrderItemRequest> Items);
    public record OrderItemRequest(Guid GoodId, int Quantity);
    public record UpdateStatusRequest(OrderStatus Status);
}
