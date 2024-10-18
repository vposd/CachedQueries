using MediatR;
using Ordering.Application.Common.Contracts;

namespace Ordering.Application.Orders.Queries.GetOrders;

public class GetOrdersQuery : IRequest<OrderDto[]>
{
}
