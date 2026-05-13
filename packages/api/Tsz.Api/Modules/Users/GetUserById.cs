using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto?>;

public sealed class GetUserByIdHandler(IUnitOfWork uow)
    : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    public Task<UserDto?> HandleAsync(GetUserByIdQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<User>()
            .FirstOrDefaultAsDtoAsync<UserDto>(u => u.Id == query.Id, ct);
}
