using MediatR;
using Ordering.Domain;
using Ordering.Infrastructure;

namespace Ordering.Application.Customers.CreateCustomer;

public class CreateCustomerCommandHandler(OrderingContext context) : IRequestHandler<CreateCustomerCommand, long>
{
    public async Task<long> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var eo = new Customer { Name = request.Name, Email = request.Email };
        context.Customers.Add(eo);
        await context.SaveChangesAsync(cancellationToken);

        return eo.Id;
    }
}
