namespace Tsz.Infrastructure.Cqrs;

public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct);
}
