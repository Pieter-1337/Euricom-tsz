using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Contracts.Queries;

namespace Tsz.Modules.Contracts.Features;

public sealed record CreateContractCommand(
    string Subject,
    Guid CustomerId,
    DateOnly Start,
    DateOnly? End)
    : ICommand<ContractDto>;

public sealed class CreateContractValidator : AbstractValidator<CreateContractCommand>
{
    private readonly ICustomersAccessModule _customers;

    public CreateContractValidator(ICustomersAccessModule customers)
    {
        _customers = customers;

        RuleFor(x => x.Subject).NotEmpty().MaximumLength(256);

        RuleFor(x => x)
            .Must(x => x.End is null || x.End.Value >= x.Start)
            .WithError(ContractErrors.EndBeforeStart);

        RuleFor(x => x.CustomerId)
            .MustAsync(CustomerExists)
                .WithError(ContractErrors.CustomerNotFound);
    }

    private Task<bool> CustomerExists(Guid id, CancellationToken ct) =>
        _customers.ExecuteQueryAsync(new CustomerExistsQuery(id), ct);
}

public sealed class CreateContractHandler(IUnitOfWork uow, ICustomersAccessModule customers)
    : ICommandHandler<CreateContractCommand, ContractDto>
{
    public async Task<ContractDto> HandleAsync(CreateContractCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<Contract>();

        var existing = await repo.GetAllAsListAsync(ct: ct, ignoreQueryFilters: true);
        var nextNumber = existing.Count() == 0 ? 1 : existing.Max(c => c.Number) + 1;

        var clientManagerId = await customers.ExecuteQueryAsync(
            new GetCustomerClientManagerIdQuery(command.CustomerId), ct);

        var contract = Contract.Create(
            nextNumber,
            command.Subject.Trim(),
            command.CustomerId,
            clientManagerId,
            command.Start,
            command.End);

        repo.Add(contract);
        await uow.SaveChangesAsync(ct);
        return ContractDto.ToDto(contract);
    }
}
