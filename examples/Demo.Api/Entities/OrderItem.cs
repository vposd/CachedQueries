using System.Text.Json.Serialization;

namespace Demo.Api.Entities;

public class OrderItem
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public Guid OrderId { get; set; }
    [JsonIgnore]
    public Order Order { get; set; } = null!;

    public Guid GoodId { get; set; }
    public Good Good { get; set; } = null!;
}
