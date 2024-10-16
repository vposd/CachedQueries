using Microsoft.EntityFrameworkCore;

namespace CachedQueries.Test;

public class Root
{
    public long Id { get; set; }
}

public class Order : Root
{
    public string? Number { get; set; }
    public long CustomerId { get; set; }
    public Customer Customer { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : Root
{
    public string? Name { get; set; }
    public ICollection<Attribute> Attributes { get; set; }
}

public class Customer : Root
{
    public string? Name { get; set; }
}

public class Attribute
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string? Text { get; set; }
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Attribute> Attributes { get; set; }
    public virtual DbSet<Customer> Customers { get; set; }
    public virtual DbSet<Order> Orders { get; set; }
}
