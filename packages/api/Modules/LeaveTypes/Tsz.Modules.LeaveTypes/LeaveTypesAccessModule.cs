using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Domain.LeaveTypes;
using Tsz.Modules.LeaveTypes.Domain.Leaves;

namespace Tsz.Modules.LeaveTypes;

internal sealed class LeaveTypesAccessModule(IDispatcher dispatcher, IUnitOfWork uow) : ILeaveTypesAccessModule
{
    public Task<bool> LeaveTypeExistsAsync(Guid leaveTypeId, CancellationToken ct = default) =>
        dispatcher.SendAsync(new LeaveTypeExistsQuery(leaveTypeId), ct);

    public Task<IReadOnlyList<ActiveLeaveTypeDto>> GetActiveLeaveTypesAsync(CancellationToken ct = default) =>
        dispatcher.SendAsync(new GetActiveLeaveTypesQuery(), ct);

    public Task<decimal?> GetUserLeaveAllowanceAsync(Guid userId, Guid leaveTypeId, int year, CancellationToken ct = default) =>
        dispatcher.SendAsync(new GetUserLeaveAllowanceQuery(userId, leaveTypeId, year), ct);

    public async Task SeedUserLeavesAsync(Guid userId, int year, CancellationToken ct = default)
    {
        var leaveTypes = await uow.RepositoryFor<LeaveType>().GetAllAsListAsync(ct: ct);
        var leaveRepo = uow.RepositoryFor<UserLeave>();
        foreach (var lt in leaveTypes)
        {
            leaveRepo.Add(UserLeave.Create(userId, lt.Id, year, lt.DefaultDays));
        }
    }
}
