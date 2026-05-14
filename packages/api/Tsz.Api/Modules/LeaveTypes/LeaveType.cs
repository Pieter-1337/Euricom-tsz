using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.LeaveTypes;

public class LeaveType : IEntityBase
{
    public Guid Id { get; set; }
    public string Name { get; private set; } = string.Empty;
    public string? PayrollCode { get; private set; }
    public string? ReportingCode { get; private set; }
    public string? Group { get; private set; }
    public int? PrioInGroup { get; private set; }
    public decimal? DefaultDays { get; private set; }
    public LeaveAllowed DefaultAllowed { get; private set; }

    private LeaveType() { }

    public static LeaveType Create(
        string name,
        LeaveAllowed defaultAllowed,
        decimal? defaultDays = null,
        string? payrollCode = null,
        string? reportingCode = null,
        string? group = null,
        int? prioInGroup = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        DefaultAllowed = defaultAllowed,
        DefaultDays = defaultDays,
        PayrollCode = payrollCode,
        ReportingCode = reportingCode,
        Group = group,
        PrioInGroup = prioInGroup,
    };

    public void Update(
        string name,
        LeaveAllowed defaultAllowed,
        decimal? defaultDays,
        string? payrollCode,
        string? reportingCode,
        string? group,
        int? prioInGroup)
    {
        Name = name;
        DefaultAllowed = defaultAllowed;
        DefaultDays = defaultDays;
        PayrollCode = payrollCode;
        ReportingCode = reportingCode;
        Group = group;
        PrioInGroup = prioInGroup;
    }
}
