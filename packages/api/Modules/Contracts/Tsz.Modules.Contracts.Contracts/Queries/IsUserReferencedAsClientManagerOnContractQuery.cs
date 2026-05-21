using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Contracts.Contracts.Queries;

public sealed record IsUserReferencedAsClientManagerOnContractQuery(Guid UserId) : IQuery<bool>;
