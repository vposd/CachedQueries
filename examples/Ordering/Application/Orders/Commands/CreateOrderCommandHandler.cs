using MediatR;
using Ordering.Domain;
using Ordering.Infrastructure;

namespace Ordering.Application.Orders.Commands;

public class CreateOrderCommandHandler(OrderingContext context) : IRequestHandler<CreateOrderCommand, long>
{
    public async Task<long> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var eo = new Order
        {
            CustomerId = request.CustomerId,
            OrderNumber = request.OrderNumber,
            OrderDate = DateTimeOffset.UtcNow,
            OrderItems = request.OrderItems.Select(x => new OrderItem
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Discount = x.Discount,
                Total = x.Quantity * x.UnitPrice - x.Discount
            }).ToHashSet()
        };

        context.Orders.Add(eo);
        await context.SaveChangesAsync(cancellationToken);

        return eo.Id;
    }
}
