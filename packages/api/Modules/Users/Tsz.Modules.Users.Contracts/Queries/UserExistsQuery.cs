using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Users.Contracts.Queries;

public sealed record UserExistsQuery(Guid UserId) : IQuery<bool>;
