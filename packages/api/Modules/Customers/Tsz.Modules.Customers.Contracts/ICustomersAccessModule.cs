using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Customers.Contracts;

public interface ICustomersAccessModule
{
    Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
    Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
}
