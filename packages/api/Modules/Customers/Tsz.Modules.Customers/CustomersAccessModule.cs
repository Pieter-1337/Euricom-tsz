using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Modules.Customers.Contracts;

namespace Tsz.Modules.Customers;

internal sealed class CustomersAccessModule(IDispatcher dispatcher) : ICustomersAccessModule
{
    public Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        dispatcher.SendAsync(query, ct);

    public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
        dispatcher.SendAsync(command, ct);
}
