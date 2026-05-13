using Tsz.Api.Common.Auth;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public sealed record GetCurrentUserQuery : IQuery<UserDto?>;

public sealed class GetCurrentUserHandler(ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, UserDto?>
{
    public async Task<UserDto?> HandleAsync(GetCurrentUserQuery query, CancellationToken ct = default)
    {
        var user = await currentUser.GetAsync(ct);
        return user is null ? null : UserDto.ToDto(user);
    }
}
