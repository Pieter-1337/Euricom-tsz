using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Customers;

public class Customer : IEntityBase
{
    public Guid Id { get; set; }
    public int Number { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Address Address { get; private set; } = Address.Empty;
    public ContactPerson ContactPerson { get; private set; } = default!;
    public DateTimeOffset? DeletedAt { get; private set; }

    private Customer() { }

    public static Customer Create(int number, string name, ContactPerson contactPerson, Address? address = null) => new()
    {
        Id = Guid.NewGuid(),
        Number = number,
        Name = name,
        ContactPerson = contactPerson,
        Address = address ?? Address.Empty,
    };

    public void Rename(string name) => Name = name;
    public void UpdateAddress(Address address) => Address = address;
    public void UpdateContact(ContactPerson contactPerson) => ContactPerson = contactPerson;
    public void SoftDelete(DateTimeOffset at) => DeletedAt = at;
}
