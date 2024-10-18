using Microsoft.EntityFrameworkCore;

namespace CachedQueries.Test.TestHelpers;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Attribute> Attributes { get; set; }
    public virtual DbSet<Customer> Customers { get; set; }
    public virtual DbSet<Order> Orders { get; set; }
}
