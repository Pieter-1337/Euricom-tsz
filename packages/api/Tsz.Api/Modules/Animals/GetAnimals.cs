using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Animals;

public sealed record GetAnimalsQuery : IQuery<IReadOnlyList<AnimalDto>>;

public sealed class GetAnimalsHandler(IUnitOfWork uow)
    : IQueryHandler<GetAnimalsQuery, IReadOnlyList<AnimalDto>>
{
    public async Task<IReadOnlyList<AnimalDto>> HandleAsync(GetAnimalsQuery query, CancellationToken ct = default)
    {
        var animals = await uow.RepositoryFor<Animal>().GetAllAsDtosAsync<AnimalDto>(ct: ct);
        return animals.ToList();
    }
}
