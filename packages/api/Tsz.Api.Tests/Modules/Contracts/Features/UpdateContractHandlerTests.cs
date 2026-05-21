using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class UpdateContractHandlerTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IRepository<Contract>> repo) BuildMocks(Contract existing)
    {
        var repo = new Mock<IRepository<Contract>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(existing);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Contract>()).Returns(repo.Object);
        return (uow, repo);
    }

    [Fact]
    public async Task HandleAsync_UpdatesFields_TrimsSubject_AndSavesOnce()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(7)
            .WithId(id)
            .WithSubject("Old subject")
            .WithClientManager(Guid.NewGuid())
            .WithPeriod(new DateOnly(2026, 1, 1), null);
        var originalCustomerId = existing.CustomerId;

        var (uow, _) = BuildMocks(existing);
        var handler = new UpdateContractHandler(uow.Object);

        var newManagerId = Guid.NewGuid();
        var dto = await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "  Engagement Renamed  ",
            ClientManagerId: newManagerId,
            Start: new DateOnly(2026, 3, 1),
            End: new DateOnly(2026, 12, 31),
            ConsultantIds: []));

        dto.Id.ShouldBe(id);
        dto.Number.ShouldBe(7);
        dto.Subject.ShouldBe("Engagement Renamed");
        dto.ClientManagerId.ShouldBe(newManagerId);
        dto.Start.ShouldBe(new DateOnly(2026, 3, 1));
        dto.End.ShouldBe(new DateOnly(2026, 12, 31));
        dto.CustomerId.ShouldBe(originalCustomerId);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ClearsEnd_WhenNull()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1));

        var (uow, _) = BuildMocks(existing);
        var handler = new UpdateContractHandler(uow.Object);

        var dto = await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "Same",
            ClientManagerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: []));

        dto.End.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_ReplacesConsultants_FromInput()
    {
        var id = Guid.NewGuid();
        var existingConsultant = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithConsultants(existingConsultant);

        var (uow, _) = BuildMocks(existing);
        var handler = new UpdateContractHandler(uow.Object);

        var newConsultant = Guid.NewGuid();
        var dto = await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "Same",
            ClientManagerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: [existingConsultant, newConsultant]));

        dto.ConsultantIds.ShouldBe(new[] { existingConsultant, newConsultant }, ignoreOrder: true);
    }

    [Fact]
    public async Task HandleAsync_RemovesConsultant_WhenOmittedFromInput()
    {
        var id = Guid.NewGuid();
        var consultantA = Guid.NewGuid();
        var consultantB = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithConsultants(consultantA, consultantB);

        var (uow, _) = BuildMocks(existing);
        var handler = new UpdateContractHandler(uow.Object);

        var dto = await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "Same",
            ClientManagerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: [consultantA]));

        dto.ConsultantIds.ShouldBe(new[] { consultantA });
    }

    [Fact]
    public async Task HandleAsync_WithNullTasks_LeavesTasksUntouched()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(1).WithId(id)
            .WithTasks(("Build", 100m));

        var (uow, _) = BuildMocks(existing);
        var handler = new UpdateContractHandler(uow.Object);

        var dto = await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "Same",
            ClientManagerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: [],
            Tasks: null));

        dto.Tasks.Count.ShouldBe(1);
        dto.Tasks.Single().Name.ShouldBe("Build");
        dto.Tasks.Single().DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithTasks_AppliesReconciliation()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(1).WithId(id)
            .WithTasks(("Build", 100m));
        var buildId = existing.Tasks.Single().Id;

        var (uow, _) = BuildMocks(existing);
        var handler = new UpdateContractHandler(uow.Object);

        var dto = await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "Same",
            ClientManagerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: [],
            Tasks: [
                new UpdateContractTaskDto(buildId, "Build v2", 120m),
                new UpdateContractTaskDto(null, "Design", 90m),
            ]));

        dto.Tasks.Count.ShouldBe(2);
        dto.Tasks.ShouldContain(t => t.Id == buildId && t.Name == "Build v2" && t.Rate == 120m);
        dto.Tasks.ShouldContain(t => t.Name == "Design" && t.Rate == 90m && t.DeletedAt == null);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyTasks_SoftDeletesAllActive()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(1).WithId(id)
            .WithTasks(("Build", 100m), ("Design", 90m));

        var (uow, _) = BuildMocks(existing);
        var handler = new UpdateContractHandler(uow.Object);

        var dto = await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "Same",
            ClientManagerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: [],
            Tasks: []));

        dto.Tasks.Count.ShouldBe(2);
        dto.Tasks.ShouldAllBe(t => t.DeletedAt != null);
    }
}
