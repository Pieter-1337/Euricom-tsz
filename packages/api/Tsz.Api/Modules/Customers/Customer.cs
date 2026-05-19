using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Customers;

public class Customer : IEntityBase
{
    public Guid Id { get; set; }
    public string Name { get; private set; } = string.Empty;

    private Customer() { }

    public static Customer Create(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
    };

    public void Rename(string name) => Name = name;
}
