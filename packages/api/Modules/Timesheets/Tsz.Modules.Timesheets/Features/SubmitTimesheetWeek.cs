using FluentValidation;
using FluentValidation.Results;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Workdays.Contracts;

namespace Tsz.Modules.Timesheets.Features;

public sealed record SubmitTimesheetWeekCommand(Guid UserId, int IsoYear, int IsoWeek)
    : ICommand<TimesheetWeekDto>;

public sealed class SubmitTimesheetWeekValidator : AbstractValidator<SubmitTimesheetWeekCommand>
{
    public SubmitTimesheetWeekValidator(IUnitOfWork uow)
    {
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                var week = await uow.RepositoryFor<TimesheetWeek>().FirstOrDefaultAsync(
                    w => w.UserId == cmd.UserId && w.IsoYear == cmd.IsoYear && w.IsoWeek == cmd.IsoWeek, ct);
                if (week is null) return true; // NotFound handled separately
                return week.Status == TimesheetStatus.Draft;
            })
            .WithError(TimesheetErrors.NotSubmittable)
            .OverridePropertyName(string.Empty);
    }
}

public sealed class SubmitTimesheetWeekHandler(
    IUnitOfWork uow,
    IContractsAccessModule contracts,
    IWorkdaysAccessModule workdays)
    : ICommandHandler<SubmitTimesheetWeekCommand, TimesheetWeekDto>
{
    public async Task<TimesheetWeekDto> HandleAsync(SubmitTimesheetWeekCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<TimesheetWeek>();
        var week = await repo.FirstOrDefaultAsync(
            w => w.UserId == command.UserId && w.IsoYear == command.IsoYear && w.IsoWeek == command.IsoWeek, ct);

        if (week is null)
            throw new Tsz.Infrastructure.Errors.ValidationException(
                [new ValidationFailure(string.Empty, TimesheetErrors.NotFound.Message)
                    { CustomState = TimesheetErrors.NotFound }]);

        week.Submit();
        await uow.SaveChangesAsync(ct);

        return await TimesheetWeekDtoBuilder.BuildAsync(week, command.IsoYear, command.IsoWeek, workdays, contracts, ct);
    }
}
