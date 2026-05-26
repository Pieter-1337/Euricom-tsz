using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Timesheets.Domain.Holidays;

namespace Tsz.Modules.Timesheets.Features;

public sealed record GetHolidaysInYearQuery(int Year)
    : IQuery<IReadOnlyList<HolidayDto>>;

public sealed class GetHolidaysInYearHandler(IUnitOfWork uow)
    : IQueryHandler<GetHolidaysInYearQuery, IReadOnlyList<HolidayDto>>
{
    public async Task<IReadOnlyList<HolidayDto>> HandleAsync(
        GetHolidaysInYearQuery query,
        CancellationToken ct = default)
    {
        var yearStart = new DateOnly(query.Year, 1, 1);
        var yearEnd = new DateOnly(query.Year + 1, 1, 1);

        var holidays = await uow.RepositoryFor<Holiday>()
            .GetAllAsListAsync(
                h => h.Country == "BE" && h.Date >= yearStart && h.Date < yearEnd,
                ct);

        return holidays
            .OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Date, h.Name, h.Type))
            .ToList();
    }
}

public sealed record HolidayDto(DateOnly Date, string Name, HolidayType Type);
