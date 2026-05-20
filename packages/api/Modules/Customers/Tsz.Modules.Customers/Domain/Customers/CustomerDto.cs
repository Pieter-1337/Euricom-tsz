using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Customers.Domain.Customers;

public sealed record AddressDto(string? Street, string? Zip, string? City, string? Country);

public sealed record ContactPersonDto(string? Name, string Email);

public sealed record CustomerDto(
    Guid Id,
    int Number,
    string Name,
    AddressDto Address,
    ContactPersonDto ContactPerson,
    Guid? ClientManagerId)
    : IEntityDto<Customer, CustomerDto>
{
    public static Expression<Func<Customer, CustomerDto>> Project =>
        c => new CustomerDto(
            c.Id,
            c.Number,
            c.Name,
            new AddressDto(c.Address.Street, c.Address.Zip, c.Address.City, c.Address.Country),
            new ContactPersonDto(c.ContactPerson.Name, c.ContactPerson.Email),
            c.ClientManagerId);

    public static CustomerDto ToDto(Customer entity) =>
        new(
            entity.Id,
            entity.Number,
            entity.Name,
            new AddressDto(entity.Address.Street, entity.Address.Zip, entity.Address.City, entity.Address.Country),
            new ContactPersonDto(entity.ContactPerson.Name, entity.ContactPerson.Email),
            entity.ClientManagerId);
}
