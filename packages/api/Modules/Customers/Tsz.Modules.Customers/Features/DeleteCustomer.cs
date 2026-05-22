using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Auth.Validation;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.Features;

public sealed record DeleteCustomerCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteCustomerValidator : ScopedRequestValidator<DeleteCustomerCommand>
{
    public DeleteCustomerValidator(IUnitOfWork uow, IDataScopeAccessor scope, ICurrentUserResolver currentUser)
        : base(uow, scope, currentUser)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleForOwnedEntity(x => x.Id, GetCustomersPagedHandler.ScopePolicy, id => c => c.Id == id)
            .WithError(CustomerErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
    }
}

public sealed class DeleteCustomerHandler(IUnitOfWork uow, TimeProvider time)
    : ICommandHandler<DeleteCustomerCommand, Unit>
{
    public async Task<Unit> HandleAsync(DeleteCustomerCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<Customer>();
        var customer = await repo.GetByIdAsync(command.Id, ct);
        customer!.SoftDelete(time.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return default;
    }
}
