using System.Globalization;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;
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
        CancellationToken ct)
    {
        var weekStart = ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday);
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
