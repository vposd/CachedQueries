namespace Ordering.Domain;

public class Order : Entity
{
    public DateTimeOffset OrderDate { get; set; }
    public string OrderNumber { get; set; }
    public long CustomerId { get; set; }
    public Customer Customer { get; set; }
    public HashSet<OrderItem> OrderItems { get; set; } = new();
}
