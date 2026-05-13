using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public class User : IEntityBase
{
    public const decimal DefaultHolidayDays = 20m;
    public const decimal DefaultAdvDays = 5m;
    public const decimal DefaultAncienniteitDays = 0m;
    public const decimal DefaultSicknessDays = 0m;

    public Guid Id { get; set; }
    public string? EntraOid { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public decimal HolidayDays { get; private set; }
    public decimal AdvDays { get; private set; }
    public decimal AncienniteitDays { get; private set; }
    public decimal SicknessDays { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private User() { }

    public static User Create(string name, string email, UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Email = email,
        Role = role,
        HolidayDays = DefaultHolidayDays,
        AdvDays = DefaultAdvDays,
        AncienniteitDays = DefaultAncienniteitDays,
        SicknessDays = DefaultSicknessDays,
    };

    public void Rename(string name) => Name = name;
    public void ChangeRole(UserRole role) => Role = role;
    public void LinkEntraOid(string oid) => EntraOid = oid;
    public void SoftDelete(DateTimeOffset at) => DeletedAt = at;
}
