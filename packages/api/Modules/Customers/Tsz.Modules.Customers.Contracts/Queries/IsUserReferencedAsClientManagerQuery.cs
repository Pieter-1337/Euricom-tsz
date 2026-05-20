using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Customers.Contracts.Queries;

public sealed record IsUserReferencedAsClientManagerQuery(Guid UserId) : IQuery<bool>;
