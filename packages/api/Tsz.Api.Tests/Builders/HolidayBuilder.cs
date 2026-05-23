using Tsz.Modules.Workdays.Contracts;
using Tsz.Modules.Workdays.Domain.Holidays;

namespace Tsz.Api.Tests.Builders;

public static class HolidayBuilder
{
    public static Holiday Build(DateOnly? date = null, string country = "BE", HolidayType type = HolidayType.Public)
    {
        var holiday = Holiday.Create(
            date ?? new DateOnly(2026, 1, 1),
            "Test Holiday",
            country,
            type);
        holiday.Id = Guid.NewGuid();
        return holiday;
    }

    public static Holiday WithId(this Holiday entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }
}
