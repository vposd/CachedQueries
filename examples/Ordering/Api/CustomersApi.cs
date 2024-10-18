using Microsoft.AspNetCore.Http.HttpResults;
using Ordering.Application.Customers.CreateCustomer;

namespace Ordering.Api;

public static class CustomersApi
{
    public static RouteGroupBuilder MapCustomers(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/customers");
        api.MapPost("/", CreateCustomerAsync);
        return api;
    }

    private static async Task<Results<Ok<long>, BadRequest<string>>> CreateCustomerAsync(
        CreateCustomerCommand request,
        [AsParameters] ApiServices os)
    {
        var result = await os.Mediator.Send(request);
        return TypedResults.Ok(result);
    }
}
