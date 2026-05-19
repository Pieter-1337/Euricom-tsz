using Tsz.Api.Modules.Customers;

namespace Tsz.Api.Tests.Builders;

public static class CustomerBuilder
{
    public static Customer Build(int number = 1)
    {
        var id = Guid.NewGuid();
        var contact = ContactPerson.Create(null, $"contact_{id.ToString()[..8]}@example.com");
        var customer = Customer.Create(number, $"Customer {number}", contact);
        customer.Id = id;
        return customer;
    }

    public static Customer WithId(this Customer entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }

    public static Customer WithName(this Customer entity, string name)
    {
        entity.Rename(name);
        return entity;
    }

    public static Customer WithContact(this Customer entity, string email, string? name = null)
    {
        entity.UpdateContact(ContactPerson.Create(name, email));
        return entity;
    }

    public static Customer WithAddress(this Customer entity, string? street = null, string? zip = null, string? city = null, string? country = null)
    {
        entity.UpdateAddress(Address.Create(street, zip, city, country));
        return entity;
    }

    public static Customer SoftDeleted(this Customer entity)
    {
        entity.SoftDelete(DateTimeOffset.UtcNow);
        return entity;
    }

    public static Customer WithClientManager(this Customer entity, Guid? userId)
    {
        entity.AssignClientManager(userId);
        return entity;
    }
}
