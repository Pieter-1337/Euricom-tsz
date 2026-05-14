using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.LeaveTypes.Features;

public sealed record UpdateLeaveTypeCommand(
    Guid Id,
    string Name,
    LeaveAllowed DefaultAllowed,
    decimal? DefaultDays,
    string? PayrollCode,
    string? ReportingCode,
    string? Group,
    int? PrioInGroup)
    : ICommand<LeaveTypeDto>;

public sealed class UpdateLeaveTypeValidator : AbstractValidator<UpdateLeaveTypeCommand>
{
    private readonly IUnitOfWork _uow;

    public UpdateLeaveTypeValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultAllowed).IsInEnum();
        RuleFor(x => x.DefaultDays).GreaterThanOrEqualTo(0).When(x => x.DefaultDays.HasValue);
        RuleFor(x => x.Id)
            .MustAsync(LeaveTypeExists).WithError(LeaveTypeErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
        RuleFor(x => x.Name)
            .MustAsync(NameNotTakenByOther).WithError(LeaveTypeErrors.NameAlreadyExists)
            .When(x => !string.IsNullOrEmpty(x.Name) && x.Id != Guid.Empty);
    }

    private async Task<bool> LeaveTypeExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<LeaveType>().ExistsAsync(lt => lt.Id == id, ct);

    private async Task<bool> NameNotTakenByOther(UpdateLeaveTypeCommand command, string name, ValidationContext<UpdateLeaveTypeCommand> ctx, CancellationToken ct) =>
        !await _uow.RepositoryFor<LeaveType>().ExistsAsync(lt => lt.Name == name && lt.Id != command.Id, ct);
}

public sealed class UpdateLeaveTypeHandler(IUnitOfWork uow)
    : ICommandHandler<UpdateLeaveTypeCommand, LeaveTypeDto>
{
    public async Task<LeaveTypeDto> HandleAsync(UpdateLeaveTypeCommand command, CancellationToken ct = default)
    {
        var lt = await uow.RepositoryFor<LeaveType>().GetByIdAsync(command.Id, ct);
        lt!.Update(
            command.Name,
            command.DefaultAllowed,
            command.DefaultDays,
            command.PayrollCode,
            command.ReportingCode,
            command.Group,
            command.PrioInGroup);

        await uow.SaveChangesAsync(ct);
        return LeaveTypeDto.ToDto(lt);
    }
}
