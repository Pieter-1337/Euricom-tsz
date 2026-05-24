using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Workdays.Contracts;

namespace Tsz.Modules.Workdays.Domain.Holidays;

public class Holiday : IEntityBase
{
    public Guid Id { get; set; }
    public DateOnly Date { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public HolidayType Type { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private Holiday() { }

    public static Holiday Create(DateOnly date, string name, string country, HolidayType type) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        Name = name,
        Country = country,
        Type = type,
    };
}
