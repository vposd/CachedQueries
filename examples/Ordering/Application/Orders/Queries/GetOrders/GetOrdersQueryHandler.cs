using CachedQueries.Core.Models;
using CachedQueries.Linq;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Common.Contracts;
using Ordering.Infrastructure;

namespace Ordering.Application.Orders.Queries.GetOrders;

public class GetOrdersQueryHandler(OrderingContext context) : IRequestHandler<GetOrdersQuery, OrderDto[]>
{
    public async Task<OrderDto[]> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await context.Orders
            .Include(x => x.Customer)
            .Include(x => x.OrderItems).ThenInclude(x => x.Product)
            .Select(x => new OrderDto
            {
                Id = x.Id,
                Customer = new CustomerDto { Name = x.Customer.Name, Email = x.Customer.Email, Id = x.Customer.Id },
                OrderDate = x.OrderDate,
                OrderNumber = x.OrderNumber,
                OrderItems = x.OrderItems.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    Product = new ProductDto { Name = i.Product.Name, Price = i.Product.Price, Id = i.Product.Id },
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Discount = i.Discount
                }).ToList()
            })
            .ToListCachedAsync(new CachingOptions(["1"]), cancellationToken);

        return orders.ToArray();
    }
}
