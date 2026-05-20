using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Modules.Users.Contracts;

namespace Tsz.Modules.Users;

internal sealed class UsersAccessModule(IDispatcher dispatcher) : IUsersAccessModule
{
    public Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        dispatcher.SendAsync(query, ct);

    public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
        dispatcher.SendAsync(command, ct);
}
