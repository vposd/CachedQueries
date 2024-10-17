using System.Transactions;
using CachedQueries.EntityFramework.Extensions;
using Microsoft.EntityFrameworkCore;
using Ordering.Domain;

namespace Ordering.Infrastructure;

public class OrderingContext(DbContextOptions<OrderingContext> options) : DbContext(options)
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        await ChangeTracker.ExpireEntitiesCacheAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }
}
