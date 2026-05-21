using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Contracts.Domain.Contracts;

public class Contract : IEntityBase
{
    public Guid Id { get; set; }
    public int Number { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid? ClientManagerId { get; private set; }
    public DateOnly Start { get; private set; }
    public DateOnly? End { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    internal List<ContractConsultant> Consultants { get; private set; } = [];

    public IReadOnlyCollection<Guid> ConsultantIds =>
        Consultants.Select(c => c.UserId).ToList();

    internal List<ContractTask> Tasks { get; private set; } = [];

    private Contract() { }

    public static Contract Create(
        int number,
        string subject,
        Guid customerId,
        Guid? clientManagerId,
        DateOnly start,
        DateOnly? end = null) => new()
    {
        Id = Guid.NewGuid(),
        Number = number,
        Subject = subject,
        CustomerId = customerId,
        ClientManagerId = clientManagerId,
        Start = start,
        End = end,
    };

    public void Rename(string subject) => Subject = subject;
    public void AssignClientManager(Guid? userId) => ClientManagerId = userId;
    public void UpdatePeriod(DateOnly start, DateOnly? end)
    {
        Start = start;
        End = end;
    }
    public void SoftDelete(DateTimeOffset at) => DeletedAt = at;

    public void ReplaceConsultants(IEnumerable<Guid> userIds)
    {
        var distinct = userIds.Distinct().ToList();
        Consultants.RemoveAll(c => !distinct.Contains(c.UserId));
        foreach (var id in distinct)
        {
            if (!Consultants.Any(c => c.UserId == id))
                Consultants.Add(new ContractConsultant { UserId = id });
        }
    }

    public void ApplyTasks(IReadOnlyList<UpdateContractTaskDto> payload, DateTimeOffset now)
    {
        var existingIds = Tasks.Select(t => t.Id).ToHashSet();
        var unknown = payload.FirstOrDefault(i => i.Id.HasValue && !existingIds.Contains(i.Id!.Value));
        if (unknown is not null)
            throw new InvalidOperationException(
                $"Task id {unknown.Id} is not on this contract.");

        var payloadIdsPresent = payload.Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        foreach (var task in Tasks.Where(t => t.DeletedAt == null && !payloadIdsPresent.Contains(t.Id)))
            task.SoftDelete(now);

        foreach (var item in payload)
        {
            if (item.Id.HasValue)
            {
                var existing = Tasks.First(t => t.Id == item.Id.Value);
                existing.Update(item.Name, item.Rate);
            }
            else
            {
                var archived = Tasks.FirstOrDefault(t =>
                    t.DeletedAt != null
                    && string.Equals(t.Name, item.Name, StringComparison.OrdinalIgnoreCase));

                if (archived is not null)
                    archived.Resurrect(item.Name, item.Rate);
                else
                    Tasks.Add(ContractTask.Create(item.Name, item.Rate));
            }
        }
    }
}

public sealed record UpdateContractTaskDto(Guid? Id, string Name, decimal Rate);
