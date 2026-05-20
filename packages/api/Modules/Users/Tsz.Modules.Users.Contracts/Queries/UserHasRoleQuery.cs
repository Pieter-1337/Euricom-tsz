using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Users.Contracts.Queries;

public sealed record UserHasRoleQuery(Guid UserId, UserRole Role) : IQuery<bool>;
