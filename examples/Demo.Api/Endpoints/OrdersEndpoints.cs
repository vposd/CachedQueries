using System.Transactions;
using CachedQueries;
using CachedQueries.Extensions;
using Demo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api.Endpoints;

public static class OrdersEndpoints
{
    private const string OrdersListTag = "orders-list";

    public static void MapOrdersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        // GET /api/orders — cached with includes.
        // The list carries a custom tag for manual invalidation. Because WithTags() replaces automatic tracking,
        // the tags of every entity the query reads are added too, so SaveChanges keeps invalidating it.
        group.MapGet("/", async (AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var orders = await db.Orders
                .Where(o => o.TenantId == tenantId)
                .Include(o => o.Customer)
                .Include(o => o.Items).ThenInclude(i => i.Good)
                .OrderByDescending(o => o.CreatedAt)
                .Cacheable(o => o
                    .Expire(TimeSpan.FromMinutes(5))
                    .WithTags(
                        OrdersListTag,
                        CacheTags.ForEntity<Order>(),
                        CacheTags.ForEntity<OrderItem>(),
                        CacheTags.ForEntity<Customer>(),
                        CacheTags.ForEntity<Good>()))
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
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var order = await db.Orders
                .Include(o => o.Customer)
                .Include(o => o.Items).ThenInclude(i => i.Good)
                .Where(o => o.Id == id && o.TenantId == tenantId)
                .Cacheable(o => o.Expire(TimeSpan.FromMinutes(10)))
                .FirstOrDefaultAsync();

            return order is null ? Results.NotFound() : Results.Ok(order);
        });

        // POST /api/orders — creates order inside an EF transaction (transaction-aware invalidation)
        group.MapPost("/", async (CreateOrderRequest req, AppDbContext db, HttpContext http) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            var (order, error) = await AddOrderAsync(db, http.GetTenantId(), req);
            if (order is null)
            {
                await transaction.RollbackAsync(); // ← discards pending invalidations
                return Results.BadRequest(new { error });
            }

            // SaveChanges inside transaction — cache invalidation is DEFERRED until commit
            await db.SaveChangesAsync();

            // Commit triggers cache invalidation for Order, OrderItem, etc.
            await transaction.CommitAsync();

            return Results.Created($"/api/orders/{order.Id}", new { order.Id });
        });

        // POST /api/orders/scoped — same, with an ambient System.Transactions.TransactionScope.
        // Invalidation runs when the scope commits; if it is disposed without Complete(), nothing is invalidated.
        group.MapPost("/scoped", async (CreateOrderRequest req, AppDbContext db, HttpContext http) =>
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            var (order, error) = await AddOrderAsync(db, http.GetTenantId(), req);
            if (order is null)
            {
                return Results.BadRequest(new { error }); // scope disposed without Complete() → rollback
            }

            await db.SaveChangesAsync(); // ← invalidation deferred until the scope commits
            scope.Complete();

            return Results.Created($"/api/orders/{order.Id}", new { order.Id });
        });

        // PUT /api/orders/{id}/status — entity loaded without .Cacheable() (cached results are read-only),
        // updated, saved: SaveChanges auto-invalidates Order queries.
        group.MapPut("/{id:guid}/status", async (Guid id, UpdateStatusRequest req, AppDbContext db, HttpContext http) =>
        {
            var tenantId = http.GetTenantId();
            var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId);
            if (order is null)
            {
                return Results.NotFound();
            }

            order.Status = req.Status;
            await db.SaveChangesAsync(); // ← auto-invalidates Order cache

            return Results.Ok(new { order.Id, order.Status });
        });

        // POST /api/orders/invalidate — manual tag-based invalidation of the orders list,
        // e.g. after another system changed orders directly in the database.
        group.MapPost("/invalidate", async () =>
        {
            await Cache.InvalidateByTagAsync(OrdersListTag);
            return Results.Ok(new { message = "Orders list cache invalidated" });
        });
    }

    private static async Task<(Order? Order, string? Error)> AddOrderAsync(
        AppDbContext db, string tenantId, CreateOrderRequest req)
    {
        var customerExists = await db.Customers.AnyAsync(c => c.Id == req.CustomerId && c.TenantId == tenantId);
        if (!customerExists)
        {
            return (null, "Customer not found");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = req.CustomerId
        };
        db.Orders.Add(order);

        foreach (var item in req.Items)
        {
            var good = await db.Goods.SingleOrDefaultAsync(g => g.Id == item.GoodId && g.TenantId == tenantId);
            if (good is null)
            {
                db.ChangeTracker.Clear();
                return (null, $"Good {item.GoodId} not found");
            }

            db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                GoodId = item.GoodId,
                Quantity = item.Quantity,
                UnitPrice = good.Price
            });
        }

        return (order, null);
    }

    public record CreateOrderRequest(Guid CustomerId, List<OrderItemRequest> Items);

    public record OrderItemRequest(Guid GoodId, int Quantity);

    public record UpdateStatusRequest(OrderStatus Status);
}
