using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Users.Contracts.Queries;

public sealed record GetUserNamesByIdsQuery(IReadOnlyList<Guid> UserIds)
    : IQuery<IReadOnlyDictionary<Guid, string>>;
