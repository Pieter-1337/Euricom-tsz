using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.Features;

public sealed record DeleteCustomerCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteCustomerValidator : AbstractValidator<DeleteCustomerCommand>
{
    private readonly IUnitOfWork _uow;

    public DeleteCustomerValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Id)
            .MustAsync(CustomerExists).WithError(CustomerErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
    }

    private async Task<bool> CustomerExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<Customer>().ExistsAsync(c => c.Id == id, ct);
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
