using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Modules.Contracts.Contracts;

namespace Tsz.Modules.Contracts;

internal sealed class ContractsAccessModule(IDispatcher dispatcher) : IContractsAccessModule
{
    public Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        dispatcher.SendAsync(query, ct);

    public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
        dispatcher.SendAsync(command, ct);
}
