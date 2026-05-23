using Tsz.Modules.LeaveTypes.Contracts.Queries;

namespace Tsz.Modules.LeaveTypes.Contracts;

public interface ILeaveTypesAccessModule
{
    Task<bool> LeaveTypeExistsAsync(Guid leaveTypeId, CancellationToken ct = default);
    Task<IReadOnlyList<ActiveLeaveTypeDto>> GetActiveLeaveTypesAsync(CancellationToken ct = default);
    Task<decimal?> GetUserLeaveAllowanceAsync(Guid userId, Guid leaveTypeId, int year, CancellationToken ct = default);

    /// <summary>
    /// Adds a UserLeave row per active LeaveType for the given user and year.
    /// Does NOT call SaveChanges — the caller is responsible for saving within the same unit of work.
    /// </summary>
    Task SeedUserLeavesAsync(Guid userId, int year, CancellationToken ct = default);
}
