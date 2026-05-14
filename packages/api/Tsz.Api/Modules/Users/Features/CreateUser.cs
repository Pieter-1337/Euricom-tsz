using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record CreateUserCommand(string Name, string Email, UserRole Role)
    : ICommand<UserDto>;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    private readonly IUnitOfWork _uow;

    public CreateUserValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Email)
            .MustAsync(EmailNotTaken).WithError(UserErrors.EmailAlreadyExists)
            .When(x => !string.IsNullOrEmpty(x.Email));
    }

    private async Task<bool> EmailNotTaken(string email, CancellationToken ct)
    {
        var exists = await _uow.RepositoryFor<User>()
            .ExistsAsync(u => u.Email == email, ct);
        return !exists;
    }
}

public sealed class CreateUserHandler(IUnitOfWork uow, TimeProvider timeProvider)
    : ICommandHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        var user = User.Create(command.Name, command.Email, command.Role);
        uow.RepositoryFor<User>().Add(user);

        var leaveTypes = await uow.RepositoryFor<LeaveType>().GetAllAsListAsync(ct: ct);
        var year = timeProvider.GetUtcNow().Year;
        var leaveRepo = uow.RepositoryFor<UserLeave>();
        foreach (var lt in leaveTypes)
        {
            leaveRepo.Add(UserLeave.Create(user.Id, lt.Id, year, lt.DefaultDays));
        }

        await uow.SaveChangesAsync(ct);
        return UserDto.ToDto(user);
    }
}
