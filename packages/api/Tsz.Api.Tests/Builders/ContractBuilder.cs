using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Api.Tests.Builders;

public static class ContractBuilder
{
    public static Contract Build(int number = 1, Guid? customerId = null)
    {
        var contract = Contract.Create(
            number,
            $"Subject {number}",
            customerId: customerId ?? Guid.NewGuid(),
            clientManagerId: Guid.NewGuid(),
            start: new DateOnly(2026, 1, 1),
            end: null);
        return contract;
    }

    public static Contract WithId(this Contract entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }

    public static Contract WithSubject(this Contract entity, string subject)
    {
        entity.Rename(subject);
        return entity;
    }

    public static Contract WithClientManager(this Contract entity, Guid userId)
    {
        entity.AssignClientManager(userId);
        return entity;
    }

    public static Contract WithPeriod(this Contract entity, DateOnly start, DateOnly? end = null)
    {
        entity.UpdatePeriod(start, end);
        return entity;
    }

    public static Contract SoftDeleted(this Contract entity)
    {
        entity.SoftDelete(DateTimeOffset.UtcNow);
        return entity;
    }

    public static Contract WithConsultants(this Contract entity, params Guid[] userIds)
    {
        entity.ReplaceConsultants(userIds);
        return entity;
    }

    public static Contract WithTasks(this Contract entity, params (string Name, decimal Rate)[] tasks)
    {
        var payload = tasks
            .Select(t => new UpdateContractTaskDto(null, t.Name, t.Rate))
            .ToList();
        entity.ApplyTasks(payload, DateTimeOffset.UtcNow);
        return entity;
    }
}
