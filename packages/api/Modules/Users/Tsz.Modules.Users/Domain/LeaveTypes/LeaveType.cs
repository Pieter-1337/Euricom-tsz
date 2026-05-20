using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Users.Domain.Leaves;

namespace Tsz.Modules.Users.Domain.LeaveTypes;

public class LeaveType : IEntityBase
{
    public Guid Id { get; set; }
    public string Name { get; private set; } = string.Empty;
    public decimal? DefaultDays { get; private set; }
    public LeaveAllowed DefaultAllowed { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private LeaveType() { }

    public static LeaveType Create(string name, LeaveAllowed defaultAllowed, decimal? defaultDays) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        DefaultAllowed = defaultAllowed,
        DefaultDays = defaultDays,
    };
}
