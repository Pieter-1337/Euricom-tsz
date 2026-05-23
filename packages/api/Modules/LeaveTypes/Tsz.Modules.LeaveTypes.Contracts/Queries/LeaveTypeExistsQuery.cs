using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.LeaveTypes.Contracts.Queries;

public sealed record LeaveTypeExistsQuery(Guid LeaveTypeId) : IQuery<bool>;
