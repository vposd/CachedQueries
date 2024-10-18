using Microsoft.AspNetCore.Http.HttpResults;
using Ordering.Application.Products.CreateProduct;

namespace Ordering.Api;

public static class ProductsApi
{
    public static RouteGroupBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/products");
        api.MapPost("/", CreateProductAsync);
        return api;
    }

    private static async Task<Results<Ok<long>, BadRequest<string>>> CreateProductAsync(
        CreateProductCommand request,
        [AsParameters] ApiServices os)
    {
        var result = await os.Mediator.Send(request);
        return TypedResults.Ok(result);
    }
}
