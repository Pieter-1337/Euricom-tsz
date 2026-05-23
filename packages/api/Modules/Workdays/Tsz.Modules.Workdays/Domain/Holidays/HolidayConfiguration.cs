using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsz.Modules.Workdays.Contracts;

namespace Tsz.Modules.Workdays.Domain.Holidays;

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Holidays");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Date).IsRequired();
        builder.Property(h => h.Name).IsRequired().HasMaxLength(256);
        builder.Property(h => h.Country).IsRequired().HasMaxLength(2);
        builder.Property(h => h.Type).IsRequired();

        builder.Ignore(h => h.DeletedAt);

        builder.HasIndex(h => new { h.Country, h.Date }).IsUnique();

        builder.HasData(BelgianHolidays());
    }

    private static IEnumerable<object> BelgianHolidays()
    {
        // Fixed holidays
        const string be = "BE";
        var h = HolidayType.Public;

        return
        [
            // 2026
            Seed("a1000001-0000-0000-0000-000000000000", new DateOnly(2026, 1, 1),  "New Year's Day",        be, h),
            Seed("a1000002-0000-0000-0000-000000000000", new DateOnly(2026, 4, 6),  "Easter Monday",         be, h),
            Seed("a1000003-0000-0000-0000-000000000000", new DateOnly(2026, 5, 1),  "Labour Day",            be, h),
            Seed("a1000004-0000-0000-0000-000000000000", new DateOnly(2026, 5, 14), "Ascension Day",         be, h),
            Seed("a1000005-0000-0000-0000-000000000000", new DateOnly(2026, 5, 25), "Whit Monday",           be, h),
            Seed("a1000006-0000-0000-0000-000000000000", new DateOnly(2026, 7, 21), "Belgian National Day",  be, h),
            Seed("a1000007-0000-0000-0000-000000000000", new DateOnly(2026, 8, 15), "Assumption of Mary",    be, h),
            Seed("a1000008-0000-0000-0000-000000000000", new DateOnly(2026, 11, 1), "All Saints' Day",       be, h),
            Seed("a1000009-0000-0000-0000-000000000000", new DateOnly(2026, 11, 11),"Armistice Day",         be, h),
            Seed("a1000010-0000-0000-0000-000000000000", new DateOnly(2026, 12, 25),"Christmas Day",         be, h),

            // 2027
            Seed("a2000001-0000-0000-0000-000000000000", new DateOnly(2027, 1, 1),  "New Year's Day",        be, h),
            Seed("a2000002-0000-0000-0000-000000000000", new DateOnly(2027, 3, 29), "Easter Monday",         be, h),
            Seed("a2000003-0000-0000-0000-000000000000", new DateOnly(2027, 5, 1),  "Labour Day",            be, h),
            Seed("a2000004-0000-0000-0000-000000000000", new DateOnly(2027, 5, 6),  "Ascension Day",         be, h),
            Seed("a2000005-0000-0000-0000-000000000000", new DateOnly(2027, 5, 17), "Whit Monday",           be, h),
            Seed("a2000006-0000-0000-0000-000000000000", new DateOnly(2027, 7, 21), "Belgian National Day",  be, h),
            Seed("a2000007-0000-0000-0000-000000000000", new DateOnly(2027, 8, 15), "Assumption of Mary",    be, h),
            Seed("a2000008-0000-0000-0000-000000000000", new DateOnly(2027, 11, 1), "All Saints' Day",       be, h),
            Seed("a2000009-0000-0000-0000-000000000000", new DateOnly(2027, 11, 11),"Armistice Day",         be, h),
            Seed("a2000010-0000-0000-0000-000000000000", new DateOnly(2027, 12, 25),"Christmas Day",         be, h),

            // 2028
            Seed("a3000001-0000-0000-0000-000000000000", new DateOnly(2028, 1, 1),  "New Year's Day",        be, h),
            Seed("a3000002-0000-0000-0000-000000000000", new DateOnly(2028, 4, 17), "Easter Monday",         be, h),
            Seed("a3000003-0000-0000-0000-000000000000", new DateOnly(2028, 5, 1),  "Labour Day",            be, h),
            Seed("a3000004-0000-0000-0000-000000000000", new DateOnly(2028, 5, 25), "Ascension Day",         be, h),
            Seed("a3000005-0000-0000-0000-000000000000", new DateOnly(2028, 6, 5),  "Whit Monday",           be, h),
            Seed("a3000006-0000-0000-0000-000000000000", new DateOnly(2028, 7, 21), "Belgian National Day",  be, h),
            Seed("a3000007-0000-0000-0000-000000000000", new DateOnly(2028, 8, 15), "Assumption of Mary",    be, h),
            Seed("a3000008-0000-0000-0000-000000000000", new DateOnly(2028, 11, 1), "All Saints' Day",       be, h),
            Seed("a3000009-0000-0000-0000-000000000000", new DateOnly(2028, 11, 11),"Armistice Day",         be, h),
            Seed("a3000010-0000-0000-0000-000000000000", new DateOnly(2028, 12, 25),"Christmas Day",         be, h),
        ];
    }

    private static object Seed(string id, DateOnly date, string name, string country, HolidayType type) =>
        new { Id = Guid.Parse(id), Date = date, Name = name, Country = country, Type = type };
}
