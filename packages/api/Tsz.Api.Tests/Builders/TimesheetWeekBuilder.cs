using Tsz.Modules.Timesheets.Domain.Timesheets;

namespace Tsz.Api.Tests.Builders;

public static class TimesheetWeekBuilder
{
    public static TimesheetWeek Build(
        Guid? userId = null,
        int isoYear = 2026,
        int isoWeek = 21) =>
        TimesheetWeek.Create(
            userId ?? Guid.NewGuid(),
            isoYear,
            isoWeek);
}
