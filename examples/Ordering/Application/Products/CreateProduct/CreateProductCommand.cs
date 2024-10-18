using MediatR;

namespace Ordering.Application.Products.CreateProduct;

public class CreateProductCommand : IRequest<long>
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}
