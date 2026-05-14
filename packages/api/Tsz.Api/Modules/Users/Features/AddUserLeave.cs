using FluentValidation;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record AddUserLeaveCommand(
    Guid UserId,
    Guid LeaveTypeId,
    decimal? TotalDays)
    : ICommand<UserLeaveDto>;

public sealed class AddUserLeaveValidator : AbstractValidator<AddUserLeaveCommand>
{
    private readonly IUnitOfWork _uow;

    public AddUserLeaveValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.LeaveTypeId).NotEmpty();
        RuleFor(x => x.TotalDays).GreaterThanOrEqualTo(0).When(x => x.TotalDays.HasValue);
        RuleFor(x => x.UserId)
            .MustAsync(UserExists).WithError(UserLeaveErrors.UserNotFound)
            .When(x => x.UserId != Guid.Empty);
        RuleFor(x => x.LeaveTypeId)
            .MustAsync(LeaveTypeExists).WithError(UserLeaveErrors.LeaveTypeNotFound)
            .When(x => x.LeaveTypeId != Guid.Empty);
        RuleFor(x => x)
            .MustAsync(NoDuplicate).WithError(UserLeaveErrors.Duplicate)
            .When(x => x.UserId != Guid.Empty && x.LeaveTypeId != Guid.Empty);
    }

    private async Task<bool> UserExists(Guid userId, CancellationToken ct) =>
        await _uow.RepositoryFor<User>().ExistsAsync(u => u.Id == userId, ct);

    private async Task<bool> LeaveTypeExists(Guid leaveTypeId, CancellationToken ct) =>
        await _uow.RepositoryFor<LeaveType>().ExistsAsync(lt => lt.Id == leaveTypeId, ct);

    private async Task<bool> NoDuplicate(AddUserLeaveCommand command, CancellationToken ct) =>
        !await _uow.RepositoryFor<UserLeave>().ExistsAsync(
            ul => ul.UserId == command.UserId && ul.LeaveTypeId == command.LeaveTypeId, ct);
}

public sealed class AddUserLeaveHandler(IUnitOfWork uow)
    : ICommandHandler<AddUserLeaveCommand, UserLeaveDto>
{
    public async Task<UserLeaveDto> HandleAsync(AddUserLeaveCommand command, CancellationToken ct = default)
    {
        var lt = await uow.RepositoryFor<LeaveType>().GetByIdAsync(command.LeaveTypeId, ct);
        var entity = UserLeave.Create(command.UserId, command.LeaveTypeId, command.TotalDays);
        uow.RepositoryFor<UserLeave>().Add(entity);
        await uow.SaveChangesAsync(ct);
        return UserLeaveDto.ToDto(entity, lt!.Name, lt.DefaultAllowed);
    }
}
