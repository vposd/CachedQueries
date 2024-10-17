using MediatR;
using Ordering.Domain;
using Ordering.Infrastructure;

namespace Ordering.Application.Products.CreateProduct;

public class CreateProductCommandHandler(OrderingContext context) : IRequestHandler<CreateProductCommand, long>
{
    public async Task<long> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var eo = new Product { Name = request.Name, Price = request.Price };
        context.Products.Add(eo);
        await context.SaveChangesAsync(cancellationToken);

        return eo.Id;
    }
}
