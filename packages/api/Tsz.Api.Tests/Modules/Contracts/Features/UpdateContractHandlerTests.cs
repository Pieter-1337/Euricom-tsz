using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;
using Tsz.Modules.Users.Contracts;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class UpdateContractHandlerTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IRepository<Contract>> repo, Mock<ICurrentUserResolver> currentUser)
        BuildMocks(Contract existing, ResolvedUser caller)
    {
        var repo = new Mock<IRepository<Contract>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(existing);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Contract>()).Returns(repo.Object);

        var currentUser = new Mock<ICurrentUserResolver>();
        currentUser.Setup(c => c.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caller);

        return (uow, repo, currentUser);
    }

    private static ResolvedUser Admin(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), new[] { nameof(UserRole.Admin) });

    private static ResolvedUser ClientManager(Guid id) =>
        new(id, new[] { nameof(UserRole.ClientManager) });

    [Fact]
    public async Task HandleAsync_AsAdmin_UpdatesFields_TrimsSubject_AndSavesOnce()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(7)
            .WithId(id)
            .WithSubject("Old subject")
            .WithClientManager(Guid.NewGuid())
            .WithPeriod(new DateOnly(2026, 1, 1), null);
        var originalCustomerId = existing.CustomerId;

        var (uow, _, currentUser) = BuildMocks(existing, Admin());
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

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
    public async Task HandleAsync_AsAdmin_ClearsEnd_WhenNull()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1));

        var (uow, _, currentUser) = BuildMocks(existing, Admin());
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

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
    public async Task HandleAsync_AsAdmin_ReplacesConsultants_FromInput()
    {
        var id = Guid.NewGuid();
        var existingConsultant = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithConsultants(existingConsultant);

        var (uow, _, currentUser) = BuildMocks(existing, Admin());
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

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
    public async Task HandleAsync_AsAdmin_RemovesConsultant_WhenOmittedFromInput()
    {
        var id = Guid.NewGuid();
        var consultantA = Guid.NewGuid();
        var consultantB = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithConsultants(consultantA, consultantB);

        var (uow, _, currentUser) = BuildMocks(existing, Admin());
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

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
    public async Task HandleAsync_AsAdmin_WithNullTasks_LeavesTasksUntouched()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(1).WithId(id)
            .WithTasks(("Build", 100m));

        var (uow, _, currentUser) = BuildMocks(existing, Admin());
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

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
    public async Task HandleAsync_AsAdmin_WithTasks_AppliesReconciliation()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(1).WithId(id)
            .WithTasks(("Build", 100m));
        var buildId = existing.Tasks.Single().Id;

        var (uow, _, currentUser) = BuildMocks(existing, Admin());
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

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
    public async Task HandleAsync_AsAdmin_WithEmptyTasks_SoftDeletesAllActive()
    {
        var id = Guid.NewGuid();
        var existing = ContractBuilder.Build(1).WithId(id)
            .WithTasks(("Build", 100m), ("Design", 90m));

        var (uow, _, currentUser) = BuildMocks(existing, Admin());
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

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

    [Fact]
    public async Task HandleAsync_AsAssignedClientManager_EditsZone2_AndSaves()
    {
        var id = Guid.NewGuid();
        var cmId = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithSubject("Engagement Alpha")
            .WithClientManager(cmId)
            .WithPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var (uow, _, currentUser) = BuildMocks(existing, ClientManager(cmId));
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

        var consultant = Guid.NewGuid();
        var dto = await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "Engagement Alpha",
            ClientManagerId: cmId,
            Start: new DateOnly(2026, 1, 1),
            End: new DateOnly(2026, 12, 31),
            ConsultantIds: [consultant],
            Tasks: [new UpdateContractTaskDto(null, "Build", 100m)]));

        dto.ConsultantIds.ShouldBe(new[] { consultant });
        dto.Tasks.Count.ShouldBe(1);
        dto.Tasks.Single().Name.ShouldBe("Build");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AsAssignedClientManager_DoesNotMutateZone1_PreservesWhitespace()
    {
        var id = Guid.NewGuid();
        var cmId = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithSubject("  Whitespaced Subject  ")
            .WithClientManager(cmId)
            .WithPeriod(new DateOnly(2026, 1, 1), null);

        var (uow, _, currentUser) = BuildMocks(existing, ClientManager(cmId));
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

        await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "  Whitespaced Subject  ",
            ClientManagerId: cmId,
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: []));

        existing.Subject.ShouldBe("  Whitespaced Subject  ");
    }

    [Fact]
    public async Task HandleAsync_AsAssignedClientManager_UnchangedZone1_PassesSilently()
    {
        var id = Guid.NewGuid();
        var cmId = Guid.NewGuid();
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 12, 31);
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithSubject("Engagement Alpha")
            .WithClientManager(cmId)
            .WithPeriod(start, end);

        var (uow, _, currentUser) = BuildMocks(existing, ClientManager(cmId));
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

        var dto = await handler.HandleAsync(new UpdateContractCommand(
            Id: id,
            Subject: "Engagement Alpha",
            ClientManagerId: cmId,
            Start: start,
            End: end,
            ConsultantIds: []));

        dto.Subject.ShouldBe("Engagement Alpha");
        dto.ClientManagerId.ShouldBe(cmId);
        dto.Start.ShouldBe(start);
        dto.End.ShouldBe(end);
    }

    [Fact]
    public async Task HandleAsync_AsAssignedClientManager_ChangedSubject_Throws()
    {
        var id = Guid.NewGuid();
        var cmId = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithSubject("Original")
            .WithClientManager(cmId)
            .WithPeriod(new DateOnly(2026, 1, 1), null);

        var (uow, _, currentUser) = BuildMocks(existing, ClientManager(cmId));
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

        var ex = await Should.ThrowAsync<FluentValidation.ValidationException>(() =>
            handler.HandleAsync(new UpdateContractCommand(
                Id: id,
                Subject: "Renamed by CM",
                ClientManagerId: cmId,
                Start: new DateOnly(2026, 1, 1),
                End: null,
                ConsultantIds: [])));

        ex.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.Zone1FieldNotEditable.Code);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AsAssignedClientManager_ChangedClientManager_Throws()
    {
        var id = Guid.NewGuid();
        var cmId = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithSubject("Engagement")
            .WithClientManager(cmId)
            .WithPeriod(new DateOnly(2026, 1, 1), null);

        var (uow, _, currentUser) = BuildMocks(existing, ClientManager(cmId));
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

        var ex = await Should.ThrowAsync<FluentValidation.ValidationException>(() =>
            handler.HandleAsync(new UpdateContractCommand(
                Id: id,
                Subject: "Engagement",
                ClientManagerId: Guid.NewGuid(),
                Start: new DateOnly(2026, 1, 1),
                End: null,
                ConsultantIds: [])));

        ex.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.Zone1FieldNotEditable.Code);
    }

    [Fact]
    public async Task HandleAsync_AsAssignedClientManager_ChangedDates_Throws()
    {
        var id = Guid.NewGuid();
        var cmId = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithSubject("Engagement")
            .WithClientManager(cmId)
            .WithPeriod(new DateOnly(2026, 1, 1), null);

        var (uow, _, currentUser) = BuildMocks(existing, ClientManager(cmId));
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

        var ex = await Should.ThrowAsync<FluentValidation.ValidationException>(() =>
            handler.HandleAsync(new UpdateContractCommand(
                Id: id,
                Subject: "Engagement",
                ClientManagerId: cmId,
                Start: new DateOnly(2026, 2, 1),
                End: null,
                ConsultantIds: [])));

        ex.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.Zone1FieldNotEditable.Code);
    }

    [Fact]
    public async Task HandleAsync_AsUnassignedClientManager_Throws()
    {
        var id = Guid.NewGuid();
        var assignedCmId = Guid.NewGuid();
        var otherCmId = Guid.NewGuid();
        var existing = ContractBuilder.Build(1)
            .WithId(id)
            .WithSubject("Engagement")
            .WithClientManager(assignedCmId)
            .WithPeriod(new DateOnly(2026, 1, 1), null);

        var (uow, _, currentUser) = BuildMocks(existing, ClientManager(otherCmId));
        var handler = new UpdateContractHandler(uow.Object, currentUser.Object);

        var ex = await Should.ThrowAsync<FluentValidation.ValidationException>(() =>
            handler.HandleAsync(new UpdateContractCommand(
                Id: id,
                Subject: "Engagement",
                ClientManagerId: assignedCmId,
                Start: new DateOnly(2026, 1, 1),
                End: null,
                ConsultantIds: [])));

        ex.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.EditNotAuthorized.Code);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
