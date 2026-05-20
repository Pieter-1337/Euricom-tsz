using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Users.Auth;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Features;

public sealed record GetCurrentUserQuery : IQuery<UserDto?>;

public sealed class GetCurrentUserHandler(ICurrentUserResolver resolver)
    : IQueryHandler<GetCurrentUserQuery, UserDto?>
{
    public async Task<UserDto?> HandleAsync(GetCurrentUserQuery query, CancellationToken ct = default)
    {
        var user = await resolver.ResolveAsync(ct);
        return user is null ? null : UserDto.ToDto(user);
    }
}
