using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Domain.Leaves;

namespace Tsz.Modules.LeaveTypes.CrossModule;

internal sealed class GetUserLeaveAllowanceQueryHandler(IUnitOfWork uow)
    : IQueryHandler<GetUserLeaveAllowanceQuery, decimal?>
{
    public async Task<decimal?> HandleAsync(GetUserLeaveAllowanceQuery query, CancellationToken ct)
    {
        var row = (await uow.RepositoryFor<UserLeave>()
            .GetAllAsListAsync(
                ul => ul.UserId == query.UserId && ul.LeaveTypeId == query.LeaveTypeId && ul.Year == query.Year,
                ct: ct))
            .FirstOrDefault();

        return row?.TotalDays;
    }
}
