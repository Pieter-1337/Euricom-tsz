using Tsz.Infrastructure.Abstractions;
using FluentValidation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record UpdateUserCommand(Guid Id, string Name, UserRole Role)
    : ICommand<UserDto?>;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class UpdateUserHandler(IUnitOfWork uow)
    : ICommandHandler<UpdateUserCommand, UserDto?>
{
    public async Task<UserDto?> HandleAsync(UpdateUserCommand command, CancellationToken ct = default)
    {
        var user = await uow.RepositoryFor<User>().GetByIdAsync(command.Id, ct);
        if (user is null) return null;

        user.Rename(command.Name);
        user.ChangeRole(command.Role);

        await uow.SaveChangesAsync(ct);
        return UserDto.ToDto(user);
    }
}
