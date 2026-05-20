using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Features;

public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto?>;

public sealed class GetUserByIdHandler(IUnitOfWork uow)
    : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    public Task<UserDto?> HandleAsync(GetUserByIdQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<User>()
            .FirstOrDefaultAsDtoAsync<UserDto>(u => u.Id == query.Id, ct);
}
