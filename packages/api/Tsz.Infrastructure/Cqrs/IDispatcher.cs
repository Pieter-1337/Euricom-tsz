using Tsz.Infrastructure.Abstractions;

namespace Tsz.Infrastructure.Cqrs;

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct = default);
}
