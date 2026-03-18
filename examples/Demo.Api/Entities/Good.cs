using System.Text.Json.Serialization;

namespace Demo.Api.Entities;

public class Good
{
    public Guid Id { get; set; }
    public required string TenantId { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public required string Category { get; set; }

    [JsonIgnore]
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
