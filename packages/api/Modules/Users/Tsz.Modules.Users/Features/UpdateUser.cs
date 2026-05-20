using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Features;

public sealed record UpdateUserCommand(Guid Id, string FirstName, string LastName, IReadOnlyCollection<UserRole> Roles)
    : ICommand<UserDto>;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ICustomersAccessModule _customers;

    public UpdateUserValidator(IUnitOfWork uow, ICustomersAccessModule customers)
    {
        _uow = uow;
        _customers = customers;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Roles).NotEmpty();
        RuleFor(x => x.Roles)
            .Must(r => r.Distinct().Count() == r.Count)
            .WithMessage("Roles must not contain duplicates.")
            .When(x => x.Roles is { Count: > 0 });
        RuleForEach(x => x.Roles).IsInEnum();
        RuleFor(x => x.Id)
            .MustAsync(UserExists).WithError(UserErrors.NotFound)
            .When(x => x.Id != Guid.Empty);

        RuleFor(x => x)
            .MustAsync(CanRemoveClientManagerRole)
            .WithError(UserErrors.CannotRemoveClientManagerRoleWhileAssigned)
            .When(x => x.Id != Guid.Empty && x.Roles is { Count: > 0 });
    }

    private async Task<bool> UserExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<User>().ExistsAsync(u => u.Id == id, ct);

    private async Task<bool> CanRemoveClientManagerRole(UpdateUserCommand cmd, CancellationToken ct)
    {
        if (cmd.Roles.Contains(UserRole.ClientManager)) return true;

        var currentlyHasRole = await _uow.RepositoryFor<User>().ExistsAsync(
            u => u.Id == cmd.Id && u.RoleAssignments.Any(r => r.Role == UserRole.ClientManager), ct);
        if (!currentlyHasRole) return true;

        var stillLinked = await _customers.ExecuteQueryAsync(new IsUserReferencedAsClientManagerQuery(cmd.Id), ct);
        return !stillLinked;
    }
}

public sealed class UpdateUserHandler(IUnitOfWork uow)
    : ICommandHandler<UpdateUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(UpdateUserCommand command, CancellationToken ct = default)
    {
        var user = await uow.RepositoryFor<User>().GetByIdAsync(command.Id, ct);
        user!.Rename(command.FirstName, command.LastName);
        user.SetRoles(command.Roles);

        await uow.SaveChangesAsync(ct);
        return UserDto.ToDto(user);
    }
}
