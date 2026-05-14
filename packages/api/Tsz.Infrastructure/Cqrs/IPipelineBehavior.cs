namespace Tsz.Infrastructure.Cqrs;

public interface IPipelineBehavior<in TCommand, TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, Func<Task<TResponse>> next, CancellationToken ct);
}
