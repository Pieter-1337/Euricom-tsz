using System.Linq.Expressions;
using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.Features;

public sealed record DeleteCustomerCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteCustomerValidator : AbstractValidator<DeleteCustomerCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IDataScopeAccessor _scope;

    public DeleteCustomerValidator(IUnitOfWork uow, IDataScopeAccessor scope)
    {
        _uow = uow;
        _scope = scope;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Id)
            .MustAsync(CustomerExistsAndAccessible).WithError(CustomerErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
    }

    private async Task<bool> CustomerExistsAndAccessible(Guid id, CancellationToken ct)
    {
        var ownership = await _scope.OwnershipFilterAsync(GetCustomersPagedHandler.ScopePolicy, ct);
        Expression<Func<Customer, bool>> filter = ownership is null
            ? c => c.Id == id
            : ownership.And(c => c.Id == id);
        return await _uow.RepositoryFor<Customer>().ExistsAsync(filter, ct);
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
