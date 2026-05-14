using Tsz.Infrastructure.Abstractions;

namespace Tsz.Infrastructure.Cqrs;

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}
