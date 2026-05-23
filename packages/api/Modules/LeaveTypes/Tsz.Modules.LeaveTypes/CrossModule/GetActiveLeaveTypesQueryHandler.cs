using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Domain.LeaveTypes;

namespace Tsz.Modules.LeaveTypes.CrossModule;

internal sealed class GetActiveLeaveTypesQueryHandler(IUnitOfWork uow)
    : IQueryHandler<GetActiveLeaveTypesQuery, IReadOnlyList<ActiveLeaveTypeDto>>
{
    public async Task<IReadOnlyList<ActiveLeaveTypeDto>> HandleAsync(GetActiveLeaveTypesQuery query, CancellationToken ct)
    {
        var leaveTypes = await uow.RepositoryFor<LeaveType>().GetAllAsListAsync(ct: ct);
        return leaveTypes
            .Select(lt => new ActiveLeaveTypeDto(lt.Id, lt.Name, lt.DefaultAllowed, lt.DefaultDays))
            .ToList();
    }
}
