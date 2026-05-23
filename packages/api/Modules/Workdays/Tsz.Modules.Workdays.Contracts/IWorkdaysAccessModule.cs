namespace Tsz.Modules.Workdays.Contracts;

public interface IWorkdaysAccessModule
{
    Task<bool> IsBusinessDay(DateOnly date, CancellationToken ct = default);
}
