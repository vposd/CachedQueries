using Microsoft.AspNetCore.Http.HttpResults;
using Ordering.Application.Common.Contracts;
using Ordering.Application.Orders.Commands;
using Ordering.Application.Orders.Queries.GetOrders;

namespace Ordering.Api;

public static class OrdersApi
{
    public static RouteGroupBuilder MapOrders(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/orders");

        api.MapGet("/", GetOrdersAsync);
        api.MapPost("/", CreateOrderAsync);

        return api;
    }

    private static async Task<Results<Ok<OrderDto[]>, BadRequest<string>>> GetOrdersAsync(
        [AsParameters] ApiServices os)
    {
        var result = await os.Mediator.Send(new GetOrdersQuery());
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<long>, BadRequest<string>>> CreateOrderAsync(
        CreateOrderCommand request,
        [AsParameters] ApiServices os)
    {
        var result = await os.Mediator.Send(request);
        return TypedResults.Ok(result);
    }
}
