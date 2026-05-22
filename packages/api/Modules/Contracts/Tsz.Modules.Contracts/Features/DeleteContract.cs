using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Auth.Validation;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Modules.Contracts.Features;

public sealed record DeleteContractCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteContractValidator : ScopedRequestValidator<DeleteContractCommand>
{
    public DeleteContractValidator(IUnitOfWork uow, IDataScopeAccessor scope, ICurrentUserResolver currentUser)
        : base(uow, scope, currentUser)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleForOwnedEntity(x => x.Id, GetContractsPagedHandler.ScopePolicy, id => c => c.Id == id)
            .WithError(ContractErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
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
