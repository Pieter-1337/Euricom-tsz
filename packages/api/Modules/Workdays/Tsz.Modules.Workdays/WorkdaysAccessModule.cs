using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Workdays.Contracts;
using Tsz.Modules.Workdays.Domain.Holidays;

namespace Tsz.Modules.Workdays;

internal sealed class WorkdaysAccessModule(IUnitOfWork uow) : IWorkdaysAccessModule
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
}
