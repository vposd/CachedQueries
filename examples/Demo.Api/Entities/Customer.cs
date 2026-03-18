using System.Text.Json.Serialization;

namespace Demo.Api.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public required string TenantId { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }

    [JsonIgnore]
    public ICollection<Order> Orders { get; set; } = [];
}
