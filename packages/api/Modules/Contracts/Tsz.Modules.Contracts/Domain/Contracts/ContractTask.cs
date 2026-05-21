namespace Tsz.Modules.Contracts.Domain.Contracts;

public class ContractTask
{
    public Guid Id { get; set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Rate { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private ContractTask() { }

    internal static ContractTask Create(string name, decimal rate) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Rate = rate,
    };

    internal void Update(string name, decimal rate)
    {
        Name = name;
        Rate = rate;
    }

    internal void Resurrect(string name, decimal rate)
    {
        Name = name;
        Rate = rate;
        DeletedAt = null;
    }

    internal void SoftDelete(DateTimeOffset at) => DeletedAt = at;
}
