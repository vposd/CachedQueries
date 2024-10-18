namespace CachedQueries.Test.TestHelpers;

public class Product : Root
{
    public string? Name { get; set; }
    public ICollection<Attribute> Attributes { get; set; }
}
