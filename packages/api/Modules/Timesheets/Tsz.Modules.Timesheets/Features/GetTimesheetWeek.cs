using System.Globalization;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Contracts.Queries;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Workdays.Contracts;

namespace Tsz.Modules.Timesheets.Features;

public sealed record GetTimesheetWeekQuery(
    Guid UserId,
    int IsoYear,
    int IsoWeek)
    : IQuery<TimesheetWeekDto>;

public sealed class GetTimesheetWeekHandler(
    IUnitOfWork uow,
    IContractsAccessModule contracts,
    IWorkdaysAccessModule workdays,
    ILeaveTypesAccessModule leaveTypes)
    : IQueryHandler<GetTimesheetWeekQuery, TimesheetWeekDto>
{
    public async Task<TimesheetWeekDto> HandleAsync(GetTimesheetWeekQuery query, CancellationToken ct = default)
    {
        var week = await uow.RepositoryFor<TimesheetWeek>()
            .FirstOrDefaultAsync(w =>
                w.UserId == query.UserId &&
                w.IsoYear == query.IsoYear &&
                w.IsoWeek == query.IsoWeek,
                ct);

        var weekStart = ISOWeek.ToDateTime(query.IsoYear, query.IsoWeek, DayOfWeek.Monday);
        var days = Enumerable.Range(0, 7)
            .Select(i => DateOnly.FromDateTime(weekStart.AddDays(i)))
            .ToArray();

        var dayKinds = await workdays.GetDayKindsAsync(days, ct);
        var dayInfos = dayKinds.Select(dk => new DayInfoDto(dk.Date, dk.IsBusinessDay, dk.HolidayName)).ToList();

        if (week is null)
            return TimesheetWeekDto.EmptyDraft(query.UserId, query.IsoYear, query.IsoWeek, dayInfos);

        var contractTaskIds = week.Entries
            .Select(e => e.ContractTaskId)
            .Distinct()
            .ToList();

        var displayInfo = contractTaskIds.Count > 0
            ? await contracts.ExecuteQueryAsync(
                new GetContractTaskDisplayInfoByIdsQuery(contractTaskIds), ct)
            : [];

        var activeLeaveTypes = week.LeaveEntries.Count > 0
            ? await leaveTypes.GetActiveLeaveTypesAsync(ct)
            : [];

        var infoById = displayInfo.ToDictionary(d => d.ContractTaskId);
        var leaveTypeById = activeLeaveTypes.ToDictionary(lt => lt.Id);

        return TimesheetWeekDto.FromEntity(week, dayInfos, infoById, leaveTypeById);
    }
}

public sealed record TimeEntryDto(
    Guid Id,
    Guid ContractTaskId,
    string TaskName,
    Guid ContractId,
    string ContractSubject,
    Guid CustomerId,
    string CustomerName,
    DateOnly Date,
    decimal DurationHours);

public sealed record LeaveBookingEntryDto(
    Guid Id,
    Guid LeaveTypeId,
    string LeaveTypeName,
    DateOnly Date,
    decimal DurationHours);

public sealed record DayInfoDto(DateOnly Date, bool IsBusinessDay, string? HolidayName = null);

public sealed record TimesheetWeekDto(
    Guid? Id,
    Guid UserId,
    int IsoYear,
    int IsoWeek,
    string Status,
    IReadOnlyList<DayInfoDto> Days,
    IReadOnlyList<TimeEntryDto> TimeEntries,
    IReadOnlyList<LeaveBookingEntryDto> LeaveBookings)
{
    internal static TimesheetWeekDto EmptyDraft(
        Guid userId,
        int isoYear,
        int isoWeek,
        IReadOnlyList<DayInfoDto> days) =>
        new(
            Id: null,
            UserId: userId,
            IsoYear: isoYear,
            IsoWeek: isoWeek,
            Status: nameof(TimesheetStatus.Draft),
            Days: days,
            TimeEntries: [],
            LeaveBookings: []);

    internal static TimesheetWeekDto FromEntity(
        TimesheetWeek week,
        IReadOnlyList<DayInfoDto> days,
        Dictionary<Guid, ContractTaskDisplayInfoDto> infoById,
        Dictionary<Guid, ActiveLeaveTypeDto> leaveTypeById) =>
        new(
            Id: week.Id,
            UserId: week.UserId,
            IsoYear: week.IsoYear,
            IsoWeek: week.IsoWeek,
            Status: week.Status.ToString(),
            Days: days,
            TimeEntries: week.Entries
                .Select(e =>
                {
                    var info = infoById.GetValueOrDefault(e.ContractTaskId);
                    return new TimeEntryDto(
                        e.Id,
                        e.ContractTaskId,
                        info?.TaskName ?? string.Empty,
                        info?.ContractId ?? Guid.Empty,
                        info?.ContractSubject ?? string.Empty,
                        info?.CustomerId ?? Guid.Empty,
                        info?.CustomerName ?? string.Empty,
                        e.Date,
                        e.DurationHours);
                })
                .ToList(),
            LeaveBookings: week.LeaveEntries
                .Select(lb =>
                {
                    var lt = leaveTypeById.GetValueOrDefault(lb.LeaveTypeId);
                    return new LeaveBookingEntryDto(
                        lb.Id,
                        lb.LeaveTypeId,
                        lt?.Name ?? string.Empty,
                        lb.Date,
                        lb.DurationHours);
                })
                .ToList());
}
