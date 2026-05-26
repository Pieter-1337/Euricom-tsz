using System.Globalization;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Contracts.Queries;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Workdays.Contracts;

namespace Tsz.Modules.Timesheets.Features;

public sealed record GetTimesheetMonthQuery(
    Guid UserId,
    int Year,
    int Month)
    : IQuery<TimesheetMonthDto>;

public sealed class GetTimesheetMonthHandler(
    IUnitOfWork uow,
    IContractsAccessModule contracts,
    IWorkdaysAccessModule workdays,
    ILeaveTypesAccessModule leaveTypes)
    : IQueryHandler<GetTimesheetMonthQuery, TimesheetMonthDto>
{
    public async Task<TimesheetMonthDto> HandleAsync(GetTimesheetMonthQuery query, CancellationToken ct = default)
    {
        // Windowing decision: include weeks whose Monday falls inside the calendar month.
        var firstDay = new DateOnly(query.Year, query.Month, 1);
        var lastDay = new DateOnly(query.Year, query.Month, DateTime.DaysInMonth(query.Year, query.Month));

        var allWeeks = await uow.RepositoryFor<TimesheetWeek>()
            .GetAllAsListAsync(
                w => w.UserId == query.UserId &&
                     w.IsoYear >= firstDay.Year - 1 &&
                     w.IsoYear <= lastDay.Year + 1,
                ct);

        // Filter: only weeks whose Monday is inside the calendar month.
        var weeks = allWeeks
            .Where(w =>
            {
                var monday = DateOnly.FromDateTime(ISOWeek.ToDateTime(w.IsoYear, w.IsoWeek, DayOfWeek.Monday));
                return monday >= firstDay && monday <= lastDay;
            })
            .OrderBy(w => w.IsoYear)
            .ThenBy(w => w.IsoWeek)
            .ToList();

        // Collect all contract task ids and leave type ids needed for display info.
        var contractTaskIds = weeks
            .SelectMany(w => w.Entries)
            .Select(e => e.ContractTaskId)
            .Distinct()
            .ToList();

        var leaveTypeIdsUsed = weeks
            .SelectMany(w => w.LeaveEntries)
            .Select(lb => lb.LeaveTypeId)
            .Any();

        var displayInfoTask = contractTaskIds.Count > 0
            ? contracts.ExecuteQueryAsync(new GetContractTaskDisplayInfoByIdsQuery(contractTaskIds), ct)
            : Task.FromResult<IReadOnlyList<ContractTaskDisplayInfoDto>>([]);

        var leaveTypesTask = leaveTypeIdsUsed
            ? leaveTypes.GetActiveLeaveTypesAsync(ct)
            : Task.FromResult<IReadOnlyList<ActiveLeaveTypeDto>>([]);

        // Enumerate all days in the month and check business day in batch.
        var allDays = Enumerable.Range(0, lastDay.DayNumber - firstDay.DayNumber + 1)
            .Select(i => firstDay.AddDays(i))
            .ToArray();

        var dayKindsTask = workdays.GetDayKindsAsync(allDays, ct);

        await Task.WhenAll(displayInfoTask, leaveTypesTask, dayKindsTask);

        var infoById = displayInfoTask.Result.ToDictionary(d => d.ContractTaskId);
        var leaveTypeById = leaveTypesTask.Result.ToDictionary(lt => lt.Id);
        var dayKindByDate = dayKindsTask.Result.ToDictionary(dk => dk.Date);

        var weekDtos = BuildWeekDtos(weeks, firstDay, lastDay, infoById, leaveTypeById, dayKindByDate);

        var monthTotalHours = weekDtos.Sum(w => w.Days.Sum(d => d.TotalHours));

        return new TimesheetMonthDto(
            UserId: query.UserId,
            Year: query.Year,
            Month: query.Month,
            Weeks: weekDtos,
            MonthTotalHours: monthTotalHours);
    }

    private static IReadOnlyList<TimesheetMonthWeekDto> BuildWeekDtos(
        IReadOnlyList<TimesheetWeek> weeks,
        DateOnly firstDay,
        DateOnly lastDay,
        Dictionary<Guid, ContractTaskDisplayInfoDto> infoById,
        Dictionary<Guid, ActiveLeaveTypeDto> leaveTypeById,
        Dictionary<DateOnly, DayKindInfo> dayKindByDate)
    {
        return weeks.Select(week =>
        {
            var weekStart = DateOnly.FromDateTime(ISOWeek.ToDateTime(week.IsoYear, week.IsoWeek, DayOfWeek.Monday));
            var weekDays = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).ToArray();

            var dayDtos = weekDays.Select(day =>
            {
                var dayKind = dayKindByDate.GetValueOrDefault(day);
                var isBusinessDay = dayKind?.IsBusinessDay ?? (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday);
                var holidayName = dayKind?.HolidayName;

                var timeEntries = week.Entries
                    .Where(e => e.Date == day)
                    .Select(e =>
                    {
                        var info = infoById.GetValueOrDefault(e.ContractTaskId);
                        return new TimesheetMonthTimeEntryDto(
                            TaskId: e.ContractTaskId,
                            TaskName: info?.TaskName ?? string.Empty,
                            ContractName: info?.ContractSubject ?? string.Empty,
                            CustomerName: info?.CustomerName ?? string.Empty,
                            DurationHours: e.DurationHours);
                    })
                    .ToList();

                var leaveBookings = week.LeaveEntries
                    .Where(lb => lb.Date == day)
                    .Select(lb =>
                    {
                        var lt = leaveTypeById.GetValueOrDefault(lb.LeaveTypeId);
                        return new TimesheetMonthLeaveBookingDto(
                            LeaveTypeId: lb.LeaveTypeId,
                            LeaveTypeName: lt?.Name ?? string.Empty,
                            DurationHours: lb.DurationHours);
                    })
                    .ToList();

                var totalHours = timeEntries.Sum(e => e.DurationHours) + leaveBookings.Sum(lb => lb.DurationHours);

                return new TimesheetMonthDayDto(
                    Date: day,
                    IsBusinessDay: isBusinessDay,
                    HolidayName: holidayName,
                    TimeEntries: timeEntries,
                    LeaveBookings: leaveBookings,
                    TotalHours: totalHours);
            }).ToList();

            var perTaskSummary = week.Entries
                .GroupBy(e => e.ContractTaskId)
                .Select(g =>
                {
                    var info = infoById.GetValueOrDefault(g.Key);
                    return new TimesheetMonthPerTaskSummaryDto(
                        ContractName: info?.ContractSubject ?? string.Empty,
                        TaskName: info?.TaskName ?? string.Empty,
                        TotalHours: g.Sum(e => e.DurationHours));
                })
                .ToList();

            var perLeaveTypeSummary = week.LeaveEntries
                .GroupBy(lb => lb.LeaveTypeId)
                .Select(g =>
                {
                    var lt = leaveTypeById.GetValueOrDefault(g.Key);
                    return new TimesheetMonthPerLeaveTypeSummaryDto(
                        LeaveTypeName: lt?.Name ?? string.Empty,
                        TotalHours: g.Sum(lb => lb.DurationHours));
                })
                .ToList();

            return new TimesheetMonthWeekDto(
                IsoYear: week.IsoYear,
                IsoWeek: week.IsoWeek,
                Status: week.Status.ToString(),
                Days: dayDtos,
                PerTaskSummary: perTaskSummary,
                PerLeaveTypeSummary: perLeaveTypeSummary);
        }).ToList();
    }
}

public sealed record TimesheetMonthDto(
    Guid UserId,
    int Year,
    int Month,
    IReadOnlyList<TimesheetMonthWeekDto> Weeks,
    decimal MonthTotalHours);

public sealed record TimesheetMonthWeekDto(
    int IsoYear,
    int IsoWeek,
    string Status,
    IReadOnlyList<TimesheetMonthDayDto> Days,
    IReadOnlyList<TimesheetMonthPerTaskSummaryDto> PerTaskSummary,
    IReadOnlyList<TimesheetMonthPerLeaveTypeSummaryDto> PerLeaveTypeSummary);

public sealed record TimesheetMonthDayDto(
    DateOnly Date,
    bool IsBusinessDay,
    string? HolidayName,
    IReadOnlyList<TimesheetMonthTimeEntryDto> TimeEntries,
    IReadOnlyList<TimesheetMonthLeaveBookingDto> LeaveBookings,
    decimal TotalHours);

public sealed record TimesheetMonthTimeEntryDto(
    Guid TaskId,
    string TaskName,
    string ContractName,
    string CustomerName,
    decimal DurationHours);

public sealed record TimesheetMonthLeaveBookingDto(
    Guid LeaveTypeId,
    string LeaveTypeName,
    decimal DurationHours);

public sealed record TimesheetMonthPerTaskSummaryDto(
    string ContractName,
    string TaskName,
    decimal TotalHours);

public sealed record TimesheetMonthPerLeaveTypeSummaryDto(
    string LeaveTypeName,
    decimal TotalHours);
