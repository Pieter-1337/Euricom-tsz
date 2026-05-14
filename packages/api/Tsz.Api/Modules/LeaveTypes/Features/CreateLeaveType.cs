using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.LeaveTypes.Features;

public sealed record CreateLeaveTypeCommand(
    string Name,
    LeaveAllowed DefaultAllowed,
    decimal? DefaultDays,
    string? PayrollCode,
    string? ReportingCode,
    string? Group,
    int? PrioInGroup)
    : ICommand<LeaveTypeDto>;

public sealed class CreateLeaveTypeValidator : AbstractValidator<CreateLeaveTypeCommand>
{
    private readonly IUnitOfWork _uow;

    public CreateLeaveTypeValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultAllowed).IsInEnum();
        RuleFor(x => x.DefaultDays).GreaterThanOrEqualTo(0).When(x => x.DefaultDays.HasValue);
        RuleFor(x => x.Name)
            .MustAsync(NameNotTaken).WithError(LeaveTypeErrors.NameAlreadyExists)
            .When(x => !string.IsNullOrEmpty(x.Name));
    }

    private async Task<bool> NameNotTaken(string name, CancellationToken ct) =>
        !await _uow.RepositoryFor<LeaveType>().ExistsAsync(lt => lt.Name == name, ct);
}

public sealed class CreateLeaveTypeHandler(IUnitOfWork uow)
    : ICommandHandler<CreateLeaveTypeCommand, LeaveTypeDto>
{
    public async Task<LeaveTypeDto> HandleAsync(CreateLeaveTypeCommand command, CancellationToken ct = default)
    {
        var lt = LeaveType.Create(
            command.Name,
            command.DefaultAllowed,
            command.DefaultDays,
            command.PayrollCode,
            command.ReportingCode,
            command.Group,
            command.PrioInGroup);

        uow.RepositoryFor<LeaveType>().Add(lt);
        await uow.SaveChangesAsync(ct);
        return LeaveTypeDto.ToDto(lt);
    }
}
