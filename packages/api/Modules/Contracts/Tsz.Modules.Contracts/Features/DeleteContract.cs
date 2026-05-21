using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Modules.Contracts.Features;

public sealed record DeleteContractCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteContractValidator : AbstractValidator<DeleteContractCommand>
{
    private readonly IUnitOfWork _uow;

    public DeleteContractValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Id)
            .MustAsync(ContractExists).WithError(ContractErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
    }

    private async Task<bool> ContractExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<Contract>().ExistsAsync(c => c.Id == id, ct);
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
