using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Timesheets.Domain.Holidays;

internal sealed class BusinessDayService(IUnitOfWork uow) : IBusinessDayService
{
    public async Task<bool> IsBusinessDay(DateOnly date, CancellationToken ct = default)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        var repo = uow.RepositoryFor<Holiday>();
        var isHoliday = await repo.ExistsAsync(
            h => h.Country == "BE" && h.Date == date,
            ct);

        return !isHoliday;
    }

    public async Task<IReadOnlyList<DayKindInfo>> GetDayKindsAsync(
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken ct = default)
    {
        var dateList = dates.ToList();
        var repo = uow.RepositoryFor<Holiday>();
        var holidays = (await repo.GetAllAsListAsync(
            h => h.Country == "BE" && dateList.Contains(h.Date),
            ct)).ToDictionary(h => h.Date);

        return dateList
            .Select(date =>
            {
                var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                holidays.TryGetValue(date, out var holiday);
                var isBusinessDay = !isWeekend && holiday is null;
                return new DayKindInfo(date, isBusinessDay, holiday?.Name);
            })
            .ToList();
    }
}
