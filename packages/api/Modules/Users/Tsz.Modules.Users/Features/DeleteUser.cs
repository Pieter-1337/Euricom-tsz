using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Features;

public sealed record DeleteUserCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteUserValidator : AbstractValidator<DeleteUserCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ICustomersAccessModule _customers;

    public DeleteUserValidator(IUnitOfWork uow, ICustomersAccessModule customers)
    {
        _uow = uow;
        _customers = customers;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Id)
            .MustAsync(UserExists).WithError(UserErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
        RuleFor(x => x.Id)
            .MustAsync(NotAssignedAsClientManager)
            .WithError(UserErrors.CannotRemoveClientManagerRoleWhileAssigned)
            .When(x => x.Id != Guid.Empty);
    }

    private async Task<bool> UserExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<User>().ExistsAsync(u => u.Id == id, ct);

    private async Task<bool> NotAssignedAsClientManager(Guid id, CancellationToken ct)
    {
        var hasRole = await _uow.RepositoryFor<User>().ExistsAsync(
            u => u.Id == id && u.RoleAssignments.Any(r => r.Role == UserRole.ClientManager), ct);
        if (!hasRole) return true;

        var stillLinked = await _customers.ExecuteQueryAsync(new IsUserReferencedAsClientManagerQuery(id), ct);
        return !stillLinked;
    }
}

public sealed class DeleteUserHandler(IUnitOfWork uow, TimeProvider time)
    : ICommandHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> HandleAsync(DeleteUserCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<User>();
        var user = await repo.GetByIdAsync(command.Id, ct);
        user!.SoftDelete(time.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return default;
    }
}
