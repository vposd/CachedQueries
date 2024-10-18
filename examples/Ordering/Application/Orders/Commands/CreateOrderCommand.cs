using MediatR;

namespace Ordering.Application.Orders.Commands;

public class CreateOrderCommand : IRequest<long>
{
    public long CustomerId { get; set; }
    public string OrderNumber { get; set; }
    public OrderItemRow[] OrderItems { get; set; }

    public record OrderItemRow(long ProductId, int Quantity, decimal UnitPrice, decimal Discount);
}
