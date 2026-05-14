using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record UpdateUserCommand(Guid Id, string Name, UserRole Role)
    : ICommand<UserDto>;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    private readonly IUnitOfWork _uow;

    public UpdateUserValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum();
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
        user!.Rename(command.Name);
        user.ChangeRole(command.Role);

        await uow.SaveChangesAsync(ct);
        return UserDto.ToDto(user);
    }
}
