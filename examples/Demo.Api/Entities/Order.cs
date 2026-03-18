using System.Text.Json.Serialization;

namespace Demo.Api.Entities;

public class Order
{
    public Guid Id { get; set; }
    public required string TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public ICollection<OrderItem> Items { get; set; } = [];
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}
