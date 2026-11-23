using Mediator;

namespace HaXus.Ugnay.Common.Interfaces.Application;

// Contract only: production scope/caller-context implementation is deferred.
public interface IApplicationDispatcher
{
    ValueTask<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
    ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
}
