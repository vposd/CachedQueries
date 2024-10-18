namespace CachedQueries.Test.TestHelpers;

public class Order : Root
{
    public string? Number { get; set; }
    public long CustomerId { get; set; }
    public Customer Customer { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
