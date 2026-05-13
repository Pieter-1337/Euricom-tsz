using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users.Features;

public sealed record DeleteUserCommand(Guid Id) : ICommand<bool>;

public sealed class DeleteUserHandler(IUnitOfWork uow, TimeProvider time)
    : ICommandHandler<DeleteUserCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteUserCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<User>();
        var user = await repo.GetByIdAsync(command.Id, ct);
        if (user is null) return false;

        user.SoftDelete(time.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return true;
    }
}
