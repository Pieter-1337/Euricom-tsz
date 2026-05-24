using System.Globalization;
using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Workdays.Contracts;

namespace Tsz.Modules.Timesheets.Features;

public sealed record TimeEntryInputDto(
    Guid ContractTaskId,
    DateOnly Date,
    decimal DurationHours);

public sealed record LeaveBookingInputDto(
    Guid LeaveTypeId,
    DateOnly Date,
    decimal DurationHours);

public sealed record ApplyTimesheetWeekBookingsCommand(
    Guid UserId,
    int IsoYear,
    int IsoWeek,
    IReadOnlyList<TimeEntryInputDto> TimeEntries,
    IReadOnlyList<LeaveBookingInputDto> LeaveBookings)
    : ICommand<TimesheetWeekDto>;

public sealed class ApplyTimesheetWeekBookingsValidator
    : AbstractValidator<ApplyTimesheetWeekBookingsCommand>
{
    public ApplyTimesheetWeekBookingsValidator(
        IUnitOfWork uow,
        IWorkdaysAccessModule workdays,
        IContractsAccessModule contracts,
        ILeaveTypesAccessModule leaveTypes)
    {
        // --- Draft status check ---
        RuleFor(x => x.TimeEntries)
            .MustAsync(async (cmd, _, ctx, ct) =>
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

        // --- Time entries: date in week ---
        RuleFor(x => x.TimeEntries)
            .Must((cmd, entries) =>
            {
                var weekStart = ISOWeek.ToDateTime(cmd.IsoYear, cmd.IsoWeek, DayOfWeek.Monday);
                var weekDates = Enumerable.Range(0, 7)
                    .Select(i => DateOnly.FromDateTime(weekStart.AddDays(i)))
                    .ToHashSet();
                return entries.All(b => weekDates.Contains(b.Date));
            })
            .WithError(TimesheetErrors.DateOutsideWeek)
            .OverridePropertyName(string.Empty);

        // --- Time entries: business day ---
        RuleFor(x => x.TimeEntries)
            .MustAsync(async (cmd, entries, ctx, ct) =>
            {
                if (!entries.Any()) return true;
                var checks = await Task.WhenAll(entries.Select(b => workdays.IsBusinessDay(b.Date, ct)));
                return checks.All(r => r);
            })
            .WithError(TimesheetErrors.DateNotBusinessDay)
            .OverridePropertyName(string.Empty);

        // --- Time entries: eligible contract tasks ---
        RuleFor(x => x.TimeEntries)
            .MustAsync(async (cmd, entries, ctx, ct) =>
            {
                if (!entries.Any()) return true;
                var eligible = await contracts.ExecuteQueryAsync(
                    new GetSelectableContractTasksForConsultantInWeekQuery(cmd.UserId, cmd.IsoYear, cmd.IsoWeek),
                    ct);
                var eligibleIds = eligible.Select(e => e.ContractTaskId).ToHashSet();
                return entries.All(b => eligibleIds.Contains(b.ContractTaskId));
            })
            .WithError(TimesheetErrors.ContractTaskNotEligible)
            .OverridePropertyName(string.Empty);

        // --- Time entries: duration range ---
        RuleFor(x => x.TimeEntries)
            .Must((_, entries) => entries.All(b =>
                b.DurationHours >= 0.25m &&
                b.DurationHours <= 8.00m &&
                (b.DurationHours * 4) % 1 == 0))
            .WithError(TimesheetErrors.InvalidDurationHours)
            .OverridePropertyName(string.Empty);

        // --- Leave bookings: date in week ---
        RuleFor(x => x.LeaveBookings)
            .Must((cmd, leavs) =>
            {
                if (!leavs.Any()) return true;
                var weekStart = ISOWeek.ToDateTime(cmd.IsoYear, cmd.IsoWeek, DayOfWeek.Monday);
                var weekDates = Enumerable.Range(0, 7)
                    .Select(i => DateOnly.FromDateTime(weekStart.AddDays(i)))
                    .ToHashSet();
                return leavs.All(b => weekDates.Contains(b.Date));
            })
            .WithError(TimesheetErrors.DateOutsideWeek)
            .OverridePropertyName(string.Empty);

        // --- Leave bookings: business day ---
        RuleFor(x => x.LeaveBookings)
            .MustAsync(async (cmd, leavs, ctx, ct) =>
            {
                if (!leavs.Any()) return true;
                var checks = await Task.WhenAll(leavs.Select(b => workdays.IsBusinessDay(b.Date, ct)));
                return checks.All(r => r);
            })
            .WithError(TimesheetErrors.DateNotBusinessDay)
            .OverridePropertyName(string.Empty);

        // --- Leave bookings: leave type exists ---
        RuleFor(x => x.LeaveBookings)
            .MustAsync(async (cmd, leavs, ctx, ct) =>
            {
                if (!leavs.Any()) return true;
                var distinctIds = leavs.Select(b => b.LeaveTypeId).Distinct().ToList();
                var checks = await Task.WhenAll(distinctIds.Select(id => leaveTypes.LeaveTypeExistsAsync(id, ct)));
                return checks.All(r => r);
            })
            .WithError(TimesheetErrors.LeaveTypeNotFound)
            .OverridePropertyName(string.Empty);

        // --- Leave bookings: duration range ---
        RuleFor(x => x.LeaveBookings)
            .Must((_, leavs) => leavs.All(b =>
                b.DurationHours >= 0.25m &&
                b.DurationHours <= 8.00m &&
                (b.DurationHours * 4) % 1 == 0))
            .WithError(TimesheetErrors.InvalidDurationHours)
            .OverridePropertyName(string.Empty);

        // --- Leave bookings: yearly allowance enforcement ---
        RuleFor(x => x.LeaveBookings)
            .MustAsync(async (cmd, leavs, ctx, ct) =>
            {
                if (!leavs.Any()) return true;

                var distinctLeaveTypeIds = leavs.Select(b => b.LeaveTypeId).Distinct().ToList();

                // Load other weeks' leave bookings for this user/year (exclude current week)
                var otherWeeks = await uow.RepositoryFor<TimesheetWeek>()
                    .GetAllAsListAsync(
                        w => w.UserId == cmd.UserId &&
                             w.IsoYear == cmd.IsoYear &&
                             w.IsoWeek != cmd.IsoWeek,
                        ct);

                // Compute per-leaveTypeId allowance check (once per leaveTypeId)
                foreach (var leaveTypeId in distinctLeaveTypeIds)
                {
                    var allowanceDays = await leaveTypes.GetUserLeaveAllowanceAsync(cmd.UserId, leaveTypeId, cmd.IsoYear, ct);
                    if (allowanceDays is null) continue; // unlimited

                    var existingHours = otherWeeks
                        .SelectMany(w => w.LeaveEntries)
                        .Where(b => b.LeaveTypeId == leaveTypeId)
                        .Sum(b => b.DurationHours);

                    var proposedHours = leavs
                        .Where(b => b.LeaveTypeId == leaveTypeId)
                        .Sum(b => b.DurationHours);

                    var totalDays = (existingHours + proposedHours) / TimesheetWeek.WorkdayCapacity;

                    if (totalDays > allowanceDays.Value)
                    {
                        ctx.MessageFormatter.AppendArgument("LeaveTypeId", leaveTypeId);
                        return false;
                    }
                }

                return true;
            })
            .WithError(TimesheetErrors.LeaveAllowanceExceeded)
            .OverridePropertyName(string.Empty);
    }
}

public sealed class ApplyTimesheetWeekBookingsHandler(
    IUnitOfWork uow,
    IContractsAccessModule contracts,
    IWorkdaysAccessModule workdays,
    ILeaveTypesAccessModule leaveTypes)
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

        week.ApplyBookings(
            command.TimeEntries
                .Select(b => new TimeEntryBookingDto(b.ContractTaskId, b.Date, b.DurationHours))
                .ToList(),
            command.LeaveBookings
                .Select(b => new LeaveBookingDto(b.LeaveTypeId, b.Date, b.DurationHours))
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

        var activeLeaveTypes = week.LeaveEntries.Count > 0
            ? await leaveTypes.GetActiveLeaveTypesAsync(ct)
            : [];

        var infoById = displayInfo.ToDictionary(d => d.ContractTaskId);
        var leaveTypeById = activeLeaveTypes.ToDictionary(lt => lt.Id);
        return TimesheetWeekDto.FromEntity(week, dayInfos, infoById, leaveTypeById);
    }
}
