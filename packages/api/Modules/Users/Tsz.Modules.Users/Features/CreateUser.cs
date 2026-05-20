using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Leaves;
using Tsz.Modules.Users.Domain.LeaveTypes;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Features;

public sealed record CreateUserCommand(string FirstName, string LastName, string Email, IReadOnlyCollection<UserRole> Roles)
    : ICommand<UserDto>;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    private readonly IUnitOfWork _uow;

    public CreateUserValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Roles).NotEmpty();
        RuleFor(x => x.Roles)
            .Must(r => r.Distinct().Count() == r.Count)
            .WithMessage("Roles must not contain duplicates.")
            .When(x => x.Roles is { Count: > 0 });
        RuleForEach(x => x.Roles).IsInEnum();
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
        var user = User.Create(command.FirstName, command.LastName, command.Email, command.Roles);
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
