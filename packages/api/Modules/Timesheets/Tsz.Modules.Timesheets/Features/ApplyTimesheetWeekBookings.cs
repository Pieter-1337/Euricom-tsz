using System.Globalization;
using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Workdays.Contracts;

namespace Tsz.Modules.Timesheets.Features;

public sealed record BookingInputDto(
    Guid ContractTaskId,
    DateOnly Date,
    decimal DurationHours);

public sealed record ApplyTimesheetWeekBookingsCommand(
    Guid UserId,
    int IsoYear,
    int IsoWeek,
    IReadOnlyList<BookingInputDto> Bookings)
    : ICommand<TimesheetWeekDto>;

public sealed class ApplyTimesheetWeekBookingsValidator
    : AbstractValidator<ApplyTimesheetWeekBookingsCommand>
{
    public ApplyTimesheetWeekBookingsValidator(
        IUnitOfWork uow,
        IWorkdaysAccessModule workdays,
        IContractsAccessModule contracts)
    {
        RuleFor(x => x.Bookings)
            .MustAsync(async (cmd, bookings, ctx, ct) =>
            {
                var week = await uow.RepositoryFor<TimesheetWeek>().FirstOrDefaultAsync(
                    w => w.UserId == cmd.UserId && w.IsoYear == cmd.IsoYear && w.IsoWeek == cmd.IsoWeek,
                    ct);

                if (week is not null && week.Status != TimesheetStatus.Draft)
                {
                    ctx.MessageFormatter.AppendArgument("Error", TimesheetErrors.NotDraft.Message);
                    return false;
                }
                return true;
            })
            .WithError(TimesheetErrors.NotDraft)
            .OverridePropertyName(string.Empty);

        RuleFor(x => x.Bookings)
            .Must((cmd, bookings) =>
            {
                var weekStart = ISOWeek.ToDateTime(cmd.IsoYear, cmd.IsoWeek, DayOfWeek.Monday);
                var weekDates = Enumerable.Range(0, 7)
                    .Select(i => DateOnly.FromDateTime(weekStart.AddDays(i)))
                    .ToHashSet();
                return bookings.All(b => weekDates.Contains(b.Date));
            })
            .WithError(TimesheetErrors.DateOutsideWeek)
            .OverridePropertyName(string.Empty);

        RuleFor(x => x.Bookings)
            .MustAsync(async (cmd, bookings, ctx, ct) =>
            {
                var checks = await Task.WhenAll(bookings.Select(b => workdays.IsBusinessDay(b.Date, ct)));
                return checks.All(r => r);
            })
            .WithError(TimesheetErrors.DateNotBusinessDay)
            .OverridePropertyName(string.Empty);

        RuleFor(x => x.Bookings)
            .MustAsync(async (cmd, bookings, ctx, ct) =>
            {
                if (!bookings.Any()) return true;
                var eligible = await contracts.ExecuteQueryAsync(
                    new GetSelectableContractTasksForConsultantInWeekQuery(cmd.UserId, cmd.IsoYear, cmd.IsoWeek),
                    ct);
                var eligibleIds = eligible.Select(e => e.ContractTaskId).ToHashSet();
                return bookings.All(b => eligibleIds.Contains(b.ContractTaskId));
            })
            .WithError(TimesheetErrors.ContractTaskNotEligible)
            .OverridePropertyName(string.Empty);

        RuleFor(x => x.Bookings)
            .Must((_, bookings) => bookings.All(b =>
                b.DurationHours >= 0.25m &&
                b.DurationHours <= 8.00m &&
                (b.DurationHours * 4) % 1 == 0))
            .WithError(TimesheetErrors.InvalidDurationHours)
            .OverridePropertyName(string.Empty);
    }
}

public sealed class ApplyTimesheetWeekBookingsHandler(IUnitOfWork uow, IContractsAccessModule contracts, IWorkdaysAccessModule workdays)
    : ICommandHandler<ApplyTimesheetWeekBookingsCommand, TimesheetWeekDto>
{
    public async Task<TimesheetWeekDto> HandleAsync(
        ApplyTimesheetWeekBookingsCommand command,
        CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<TimesheetWeek>();
        var week = await repo.FirstOrDefaultAsync(
            w => w.UserId == command.UserId &&
                 w.IsoYear == command.IsoYear &&
                 w.IsoWeek == command.IsoWeek,
            ct);

        if (week is null)
        {
            week = TimesheetWeek.Create(command.UserId, command.IsoYear, command.IsoWeek);
            repo.Add(week);
        }

        week.ApplyTimeEntries(command.Bookings
            .Select(b => new TimeEntryBookingDto(b.ContractTaskId, b.Date, b.DurationHours))
            .ToList());

        await uow.SaveChangesAsync(ct);

        var weekStart = ISOWeek.ToDateTime(command.IsoYear, command.IsoWeek, DayOfWeek.Monday);
        var days = Enumerable.Range(0, 7)
            .Select(i => DateOnly.FromDateTime(weekStart.AddDays(i)))
            .ToArray();

        var businessDayChecks = await Task.WhenAll(days.Select(d => workdays.IsBusinessDay(d, ct)));
        var dayInfos = days.Select((d, i) => new DayInfoDto(d, businessDayChecks[i])).ToList();

        var contractTaskIds = week.Entries.Select(e => e.ContractTaskId).Distinct().ToList();
        var displayInfo = contractTaskIds.Count > 0
            ? await contracts.ExecuteQueryAsync(new GetContractTaskDisplayInfoByIdsQuery(contractTaskIds), ct)
            : [];

        var infoById = displayInfo.ToDictionary(d => d.ContractTaskId);
        return TimesheetWeekDto.FromEntity(week, dayInfos, infoById);
    }
}
