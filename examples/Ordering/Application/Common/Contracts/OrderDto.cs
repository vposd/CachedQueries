namespace Ordering.Application.Common.Contracts;

public class OrderDto
{
    public long Id { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public string OrderNumber { get; set; }
    public CustomerDto Customer { get; set; }
    public IEnumerable<OrderItemDto> OrderItems { get; set; }
}
