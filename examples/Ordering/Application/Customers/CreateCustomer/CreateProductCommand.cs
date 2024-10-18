using MediatR;

namespace Ordering.Application.Customers.CreateCustomer;

public class CreateCustomerCommand : IRequest<long>
{
    public string Name { get; set; }
    public string Email { get; set; }
}
