using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Domain.LeaveTypes;

namespace Tsz.Modules.LeaveTypes.CrossModule;

internal sealed class LeaveTypeExistsQueryHandler(IUnitOfWork uow)
    : IQueryHandler<LeaveTypeExistsQuery, bool>
{
    public Task<bool> HandleAsync(LeaveTypeExistsQuery query, CancellationToken ct) =>
        uow.RepositoryFor<LeaveType>().ExistsAsync(lt => lt.Id == query.LeaveTypeId, ct);
}
