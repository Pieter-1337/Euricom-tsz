using System.Linq.Expressions;
using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Modules.Contracts.Features;

public sealed record DeleteContractCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteContractValidator : AbstractValidator<DeleteContractCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IDataScopeAccessor _scope;

    public DeleteContractValidator(IUnitOfWork uow, IDataScopeAccessor scope)
    {
        _uow = uow;
        _scope = scope;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Id)
            .MustAsync(ContractExistsAndAccessible).WithError(ContractErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
    }

    private async Task<bool> ContractExistsAndAccessible(Guid id, CancellationToken ct)
    {
        var ownership = await _scope.OwnershipFilterAsync(GetContractsPagedHandler.ScopePolicy, ct);
        Expression<Func<Contract, bool>> filter = ownership is null
            ? c => c.Id == id
            : ownership.And(c => c.Id == id);
        return await _uow.RepositoryFor<Contract>().ExistsAsync(filter, ct);
    }
}

public sealed class DeleteContractHandler(IUnitOfWork uow, TimeProvider time)
    : ICommandHandler<DeleteContractCommand, Unit>
{
    public async Task<Unit> HandleAsync(DeleteContractCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<Contract>();
        var contract = await repo.GetByIdAsync(command.Id, ct);
        contract!.SoftDelete(time.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return default;
    }
}
