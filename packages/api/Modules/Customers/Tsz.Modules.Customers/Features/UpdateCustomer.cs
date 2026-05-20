using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;
using Tsz.SharedKernel;

namespace Tsz.Modules.Customers.Features;

public sealed record UpdateCustomerCommand(
    Guid Id,
    string Name,
    ContactPersonDto ContactPerson,
    AddressDto? Address,
    Guid? ClientManagerId)
    : ICommand<CustomerDto>;

public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IUsersAccessModule _users;

    public UpdateCustomerValidator(IUnitOfWork uow, IUsersAccessModule users)
    {
        _uow = uow;
        _users = users;

        RuleFor(x => x.Id).NotEmpty();
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

        RuleFor(x => x.Id)
            .MustAsync(CustomerExists).WithError(CustomerErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
    }

    private async Task<bool> CustomerExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<Customer>().ExistsAsync(c => c.Id == id, ct);

    private Task<bool> UserExists(Guid id, CancellationToken ct) =>
        _users.ExecuteQueryAsync(new UserExistsQuery(id), ct);

    private Task<bool> UserHasClientManagerRole(Guid id, CancellationToken ct) =>
        _users.ExecuteQueryAsync(new UserHasRoleQuery(id, UserRole.ClientManager), ct);
}

public sealed class UpdateCustomerHandler(IUnitOfWork uow)
    : ICommandHandler<UpdateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> HandleAsync(UpdateCustomerCommand command, CancellationToken ct = default)
    {
        var customer = await uow.RepositoryFor<Customer>().GetByIdAsync(command.Id, ct);

        customer!.Rename(command.Name.Trim());
        customer.UpdateContact(ContactPerson.Create(command.ContactPerson.Name, command.ContactPerson.Email));
        customer.UpdateAddress(command.Address is null
            ? Address.Empty
            : Address.Create(command.Address.Street, command.Address.Zip, command.Address.City, command.Address.Country));
        customer.AssignClientManager(command.ClientManagerId);

        await uow.SaveChangesAsync(ct);
        return CustomerDto.ToDto(customer);
    }
}
