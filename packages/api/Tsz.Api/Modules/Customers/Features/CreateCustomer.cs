using FluentValidation;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Customers.Features;

public sealed record CreateCustomerCommand(
    string Name,
    ContactPersonDto ContactPerson,
    AddressDto? Address)
    : ICommand<CustomerDto>;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);

        RuleFor(x => x.ContactPerson).NotNull();
        When(x => x.ContactPerson is not null, () =>
        {
            RuleFor(x => x.ContactPerson.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);
            RuleFor(x => x.ContactPerson.Name).MaximumLength(256);
        });

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Street).MaximumLength(256);
            RuleFor(x => x.Address!.Zip).MaximumLength(32);
            RuleFor(x => x.Address!.City).MaximumLength(128);
            RuleFor(x => x.Address!.Country)
                .Must(c => string.IsNullOrEmpty(c) || Countries.IsValid(c))
                .WithMessage("Country must be a valid ISO 3166-1 alpha-2 code.");
        });
    }
}

public sealed class CreateCustomerHandler(IUnitOfWork uow)
    : ICommandHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> HandleAsync(CreateCustomerCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<Customer>();

        var existing = await repo.GetAllAsListAsync(ct: ct, ignoreQueryFilters: true);
        var nextNumber = existing.Count() == 0 ? 1 : existing.Max(c => c.Number) + 1;

        var address = command.Address is null
            ? Address.Empty
            : Address.Create(command.Address.Street, command.Address.Zip, command.Address.City, command.Address.Country);

        var contact = ContactPerson.Create(command.ContactPerson.Name, command.ContactPerson.Email);

        var customer = Customer.Create(nextNumber, command.Name.Trim(), contact, address);
        repo.Add(customer);

        await uow.SaveChangesAsync(ct);
        return CustomerDto.ToDto(customer);
    }
}
