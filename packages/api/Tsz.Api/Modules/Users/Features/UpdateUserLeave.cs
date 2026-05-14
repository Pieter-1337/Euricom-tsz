using FluentValidation;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record UpdateUserLeaveCommand(
    Guid UserId,
    Guid Id,
    decimal? TotalDays)
    : ICommand<UserLeaveDto>;

public sealed class UpdateUserLeaveValidator : AbstractValidator<UpdateUserLeaveCommand>
{
    private readonly IUnitOfWork _uow;

    public UpdateUserLeaveValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TotalDays).GreaterThanOrEqualTo(0).When(x => x.TotalDays.HasValue);
        RuleFor(x => x.Id)
            .MustAsync(RowExists).WithError(UserLeaveErrors.NotFound)
            .When(x => x.Id != Guid.Empty && x.UserId != Guid.Empty);
        RuleFor(x => x)
            .MustAsync(TotalDaysMatchesAllowed)
            .WithErrorCode(CommonErrors.Invalid.Code)
            .WithMessage("TotalDays must be set for Limited leave types and null for non-Limited leave types.")
            .When(x => x.Id != Guid.Empty && x.UserId != Guid.Empty);
    }

    private async Task<bool> RowExists(UpdateUserLeaveCommand command, Guid id, ValidationContext<UpdateUserLeaveCommand> ctx, CancellationToken ct) =>
        await _uow.RepositoryFor<UserLeave>().ExistsAsync(ul => ul.Id == id && ul.UserId == command.UserId, ct);

    private async Task<bool> TotalDaysMatchesAllowed(UpdateUserLeaveCommand command, CancellationToken ct)
    {
        var entity = await _uow.RepositoryFor<UserLeave>()
            .FirstOrDefaultAsync(ul => ul.Id == command.Id && ul.UserId == command.UserId, ct);
        if (entity is null) return true; // NotFound rule already caught this

        var lt = await _uow.RepositoryFor<LeaveType>().GetByIdAsync(entity.LeaveTypeId, ct);
        if (lt is null) return true;

        if (lt.DefaultAllowed == LeaveAllowed.Limited && command.TotalDays is null) return false;
        if (lt.DefaultAllowed != LeaveAllowed.Limited && command.TotalDays is not null) return false;
        return true;
    }
}

public sealed class UpdateUserLeaveHandler(IUnitOfWork uow)
    : ICommandHandler<UpdateUserLeaveCommand, UserLeaveDto>
{
    public async Task<UserLeaveDto> HandleAsync(UpdateUserLeaveCommand command, CancellationToken ct = default)
    {
        var entity = await uow.RepositoryFor<UserLeave>()
            .FirstOrDefaultAsync(ul => ul.Id == command.Id && ul.UserId == command.UserId, ct);

        var lt = await uow.RepositoryFor<LeaveType>().GetByIdAsync(entity!.LeaveTypeId, ct);

        entity.SetTotalDays(command.TotalDays);
        await uow.SaveChangesAsync(ct);

        return UserLeaveDto.ToDto(entity, lt!.Name, lt.DefaultAllowed);
    }
}
