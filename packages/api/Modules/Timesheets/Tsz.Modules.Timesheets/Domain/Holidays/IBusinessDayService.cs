namespace Tsz.Modules.Timesheets.Domain.Holidays;

public sealed record DayKindInfo(DateOnly Date, bool IsBusinessDay, string? HolidayName);

public interface IBusinessDayService
{
    Task<bool> IsBusinessDay(DateOnly date, CancellationToken ct = default);

    Task<IReadOnlyList<DayKindInfo>> GetDayKindsAsync(
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken ct = default);
}
