using System.Globalization;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Workdays.Contracts;

namespace Tsz.Modules.Timesheets.Features;

internal static class TimesheetWeekDtoBuilder
{
    internal static async Task<TimesheetWeekDto> BuildAsync(
        TimesheetWeek week,
        int isoYear,
        int isoWeek,
        IWorkdaysAccessModule workdays,
        IContractsAccessModule contracts,
        ILeaveTypesAccessModule leaveTypes,
        CancellationToken ct)
    {
        var weekStart = ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday);
        var days = Enumerable.Range(0, 7)
            .Select(i => DateOnly.FromDateTime(weekStart.AddDays(i)))
            .ToArray();

        var dayKinds = await workdays.GetDayKindsAsync(days, ct);
        var dayInfos = dayKinds.Select(dk => new DayInfoDto(dk.Date, dk.IsBusinessDay, dk.HolidayName)).ToList();

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
