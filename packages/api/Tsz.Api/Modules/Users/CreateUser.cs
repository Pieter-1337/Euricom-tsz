using Tsz.Infrastructure.Abstractions;
using FluentValidation;

namespace Tsz.Api.Modules.Users;

public sealed record CreateUserCommand(string Name, string Email, UserRole Role)
    : ICommand<CreateUserResult>;

public sealed record CreateUserResult(UserDto? User, bool Conflict)
{
    public static CreateUserResult Created(UserDto user) => new(user, false);
    public static CreateUserResult EmailConflict() => new(null, true);
}

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class CreateUserHandler(IUnitOfWork uow)
    : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> HandleAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<User>();
        var emailLower = command.Email.ToLowerInvariant();

        if (await repo.ExistsAsync(u => u.Email.ToLower() == emailLower, ct))
            return CreateUserResult.EmailConflict();

        var user = User.Create(command.Name, command.Email, command.Role);
        repo.Add(user);
        await uow.SaveChangesAsync(ct);
        return CreateUserResult.Created(UserDto.ToDto(user));
    }
}
