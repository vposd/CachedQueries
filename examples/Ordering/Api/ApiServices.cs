using MediatR;

namespace Ordering.Api;

public class ApiServices(
    IMediator mediator)
{
    public IMediator Mediator { get; set; } = mediator;
}
