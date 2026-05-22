using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Modules.Contracts.Features;

public sealed record GetContractByIdQuery(Guid Id) : IQuery<ContractDto?>;

public sealed class GetContractByIdHandler(IUnitOfWork uow, IDataScopeAccessor scope)
    : IQueryHandler<GetContractByIdQuery, ContractDto?>
{
    public async Task<ContractDto?> HandleAsync(GetContractByIdQuery query, CancellationToken ct = default)
    {
        var ownership = await scope.OwnershipFilterAsync(GetContractsPagedHandler.ScopePolicy, ct);
        Expression<Func<Contract, bool>> filter = ownership is null
            ? c => c.Id == query.Id
            : ownership.And(c => c.Id == query.Id);

        return await uow.RepositoryFor<Contract>()
            .FirstOrDefaultAsDtoAsync<ContractDto>(filter, ct);
    }
}
