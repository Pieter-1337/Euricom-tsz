namespace Tsz.Modules.Workdays.Contracts;

public sealed record DayKindInfo(DateOnly Date, bool IsBusinessDay, string? HolidayName);

public interface IWorkdaysAccessModule
{
    Task<bool> IsBusinessDay(DateOnly date, CancellationToken ct = default);

    Task<IReadOnlyList<DayKindInfo>> GetDayKindsAsync(
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken ct = default);
}
