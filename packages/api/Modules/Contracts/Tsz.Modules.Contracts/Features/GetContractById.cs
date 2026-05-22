using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Auth.Validation;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Modules.Contracts.Features;

public sealed record GetContractByIdQuery(Guid Id) : IQuery<ContractDto?>;

public sealed class GetContractByIdHandler(IUnitOfWork uow, IDataScopeAccessor scope)
    : IQueryHandler<GetContractByIdQuery, ContractDto?>
{
    public async Task<ContractDto?> HandleAsync(GetContractByIdQuery query, CancellationToken ct = default)
    {
        var filter = await ScopedFilter.ComposeAsync(scope, GetContractsPagedHandler.ScopePolicy, c => c.Id == query.Id, ct);
        return await uow.RepositoryFor<Contract>().FirstOrDefaultAsDtoAsync<ContractDto>(filter, ct);
    }
}
