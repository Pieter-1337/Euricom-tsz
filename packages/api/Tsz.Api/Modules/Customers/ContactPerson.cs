namespace Tsz.Api.Modules.Customers;

public sealed record ContactPerson
{
    public string? Name { get; private set; }
    public string Email { get; private set; } = string.Empty;

    private ContactPerson() { }

    public static ContactPerson Create(string? name, string email) => new()
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
        Email = email.Trim(),
    };
}
