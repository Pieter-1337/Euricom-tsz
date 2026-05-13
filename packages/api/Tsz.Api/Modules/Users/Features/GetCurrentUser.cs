using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users.Features;

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
