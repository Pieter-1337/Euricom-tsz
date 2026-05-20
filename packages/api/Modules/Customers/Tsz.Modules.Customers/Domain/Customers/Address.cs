namespace Tsz.Modules.Customers.Domain.Customers;

public sealed record Address
{
    public string? Street { get; private set; }
    public string? Zip { get; private set; }
    public string? City { get; private set; }
    public string? Country { get; private set; }

    private Address() { }

    public static readonly Address Empty = new();

    public static Address Create(string? street, string? zip, string? city, string? country) => new()
    {
        Street = Normalize(street),
        Zip = Normalize(zip),
        City = Normalize(city),
        Country = Normalize(country),
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
