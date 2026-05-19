using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record UpdateUserCommand(Guid Id, string FirstName, string LastName, IReadOnlyCollection<UserRole> Roles)
    : ICommand<UserDto>;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    private readonly IUnitOfWork _uow;

    public UpdateUserValidator(IUnitOfWork uow)
    {
        _uow = uow;

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
    }

    private async Task<bool> UserExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<User>().ExistsAsync(u => u.Id == id, ct);
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
