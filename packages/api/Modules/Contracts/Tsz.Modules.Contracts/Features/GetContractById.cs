using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Modules.Contracts.Features;

public sealed record GetContractByIdQuery(Guid Id) : IQuery<ContractDto?>;

public sealed class GetContractByIdHandler(IUnitOfWork uow)
    : IQueryHandler<GetContractByIdQuery, ContractDto?>
{
    public Task<ContractDto?> HandleAsync(GetContractByIdQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<Contract>()
            .FirstOrDefaultAsDtoAsync<ContractDto>(c => c.Id == query.Id, ct);
}
