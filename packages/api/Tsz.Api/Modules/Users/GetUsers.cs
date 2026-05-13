using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public sealed record GetUsersQuery : IQuery<IReadOnlyList<UserDto>>;

public sealed class GetUsersHandler(IUnitOfWork uow)
    : IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> HandleAsync(GetUsersQuery query, CancellationToken ct = default)
    {
        var users = await uow.RepositoryFor<User>().GetAllAsDtosAsync<UserDto>(ct: ct);
        return users.ToList();
    }
}
