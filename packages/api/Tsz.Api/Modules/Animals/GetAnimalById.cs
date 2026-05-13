using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Animals;

public sealed record GetAnimalByIdQuery(Guid Id) : IQuery<AnimalDto?>;

public sealed class GetAnimalByIdHandler(IUnitOfWork uow)
    : IQueryHandler<GetAnimalByIdQuery, AnimalDto?>
{
    public Task<AnimalDto?> HandleAsync(GetAnimalByIdQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<Animal>()
            .FirstOrDefaultAsDtoAsync<AnimalDto>(a => a.Id == query.Id, ct);
}
