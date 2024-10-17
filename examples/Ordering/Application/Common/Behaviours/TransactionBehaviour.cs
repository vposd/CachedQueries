using System.Transactions;
using MediatR;

namespace Ordering.Application.Common.Behaviours;

public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var transaction = CreateScope(IsolationLevel.ReadCommitted);
        var response = await next();
        transaction.Complete();

        return response;
    }

    private static TransactionScope CreateScope(IsolationLevel level)
    {
        return Transaction.Current == null
            ? new TransactionScope(TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = level, Timeout = TimeSpan.FromMinutes(60) },
                TransactionScopeAsyncFlowOption.Enabled)
            : new TransactionScope(Transaction.Current, TransactionScopeAsyncFlowOption.Enabled);
    }
}
