using FluentValidation;
using Tsz.Api.Modules.Users;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Customers.Features;

public sealed record CreateCustomerCommand(
    string Name,
    ContactPersonDto ContactPerson,
    AddressDto? Address,
    Guid? ClientManagerId)
    : ICommand<CustomerDto>;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    private readonly IUnitOfWork _uow;

    public CreateCustomerValidator(IUnitOfWork uow)
    {
        _uow = uow;

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

        When(x => x.ClientManagerId is not null, () =>
        {
            RuleFor(x => x.ClientManagerId!.Value)
                .MustAsync(UserExists)
                    .WithError(CustomerErrors.ClientManagerNotFound)
                .MustAsync(UserHasClientManagerRole)
                    .WithError(CustomerErrors.ClientManagerMissingRole);
        });
    }

    private async Task<bool> UserExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<User>().ExistsAsync(u => u.Id == id, ct);

    private async Task<bool> UserHasClientManagerRole(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<User>().ExistsAsync(
            u => u.Id == id && u.RoleAssignments.Any(r => r.Role == UserRole.ClientManager), ct);
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

        var customer = Customer.Create(nextNumber, command.Name.Trim(), contact, address, command.ClientManagerId);
        repo.Add(customer);

        await uow.SaveChangesAsync(ct);
        return CustomerDto.ToDto(customer);
    }
}
