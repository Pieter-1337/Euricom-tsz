using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Api.Tests.Modules.Contracts.Domain;

public class ContractApplyTasksTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero);

    private static Contract MakeContract() => ContractBuilder.Build(1);

    [Fact]
    public void Case1_UpdateExistingTask_ChangesNameAndRate()
    {
        var contract = MakeContract().WithTasks(("Build", 100m));
        var existing = contract.Tasks.Single();

        contract.ApplyTasks(
            [new UpdateContractTaskDto(existing.Id, "Build (renamed)", 150m)],
            Now);

        contract.Tasks.Count.ShouldBe(1);
        var updated = contract.Tasks.Single();
        updated.Id.ShouldBe(existing.Id);
        updated.Name.ShouldBe("Build (renamed)");
        updated.Rate.ShouldBe(150m);
        updated.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Case2_ResurrectByName_CaseInsensitive_PreservesId_OverwritesCasingAndRate()
    {
        var contract = MakeContract().WithTasks(("Build", 100m));
        var archivedId = contract.Tasks.Single().Id;
        contract.ApplyTasks([], Now); // soft-deletes "Build"

        contract.ApplyTasks(
            [new UpdateContractTaskDto(null, "BUILD", 200m)],
            Now);

        var resurrected = contract.Tasks.Single();
        resurrected.Id.ShouldBe(archivedId);
        resurrected.Name.ShouldBe("BUILD");
        resurrected.Rate.ShouldBe(200m);
        resurrected.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Case3_NewTask_WhenNoNameMatch_CreatesFreshGuid()
    {
        var contract = MakeContract();

        contract.ApplyTasks(
            [new UpdateContractTaskDto(null, "Design", 90m)],
            Now);

        var created = contract.Tasks.Single();
        created.Id.ShouldNotBe(Guid.Empty);
        created.Name.ShouldBe("Design");
        created.Rate.ShouldBe(90m);
        created.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Case4_ActiveTask_MissingFromPayload_IsSoftDeleted()
    {
        var contract = MakeContract().WithTasks(("Build", 100m), ("Design", 90m));
        var build = contract.Tasks.Single(t => t.Name == "Build");
        var design = contract.Tasks.Single(t => t.Name == "Design");

        contract.ApplyTasks(
            [new UpdateContractTaskDto(build.Id, "Build", 100m)],
            Now);

        contract.Tasks.Single(t => t.Id == design.Id).DeletedAt.ShouldBe(Now);
        contract.Tasks.Single(t => t.Id == build.Id).DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Case5_UnknownPayloadId_Throws()
    {
        var contract = MakeContract().WithTasks(("Build", 100m));

        Should.Throw<InvalidOperationException>(() =>
            contract.ApplyTasks(
                [new UpdateContractTaskDto(Guid.NewGuid(), "Whatever", 50m)],
                Now));
    }

    [Fact]
    public void Mixed_Add_Delete_Update_InSameCall()
    {
        var contract = MakeContract().WithTasks(("Build", 100m), ("Design", 90m));
        var build = contract.Tasks.Single(t => t.Name == "Build");
        var design = contract.Tasks.Single(t => t.Name == "Design");

        contract.ApplyTasks(
            [
                new UpdateContractTaskDto(build.Id, "Build v2", 110m),
                new UpdateContractTaskDto(null, "Review", 70m),
            ],
            Now);

        contract.Tasks.Count.ShouldBe(3);
        contract.Tasks.Single(t => t.Id == build.Id).Name.ShouldBe("Build v2");
        contract.Tasks.Single(t => t.Id == design.Id).DeletedAt.ShouldBe(Now);
        contract.Tasks.ShouldContain(t => t.Name == "Review" && t.DeletedAt == null);
    }

    [Fact]
    public void EmptyPayload_SoftDeletesAllActiveTasks()
    {
        var contract = MakeContract().WithTasks(("A", 1m), ("B", 2m));

        contract.ApplyTasks([], Now);

        contract.Tasks.Count.ShouldBe(2);
        contract.Tasks.ShouldAllBe(t => t.DeletedAt == Now);
    }

    [Fact]
    public void PayloadIdenticalToCurrent_NoEffectiveChange()
    {
        var contract = MakeContract().WithTasks(("Build", 100m));
        var existing = contract.Tasks.Single();

        contract.ApplyTasks(
            [new UpdateContractTaskDto(existing.Id, "Build", 100m)],
            Now);

        var after = contract.Tasks.Single();
        after.Id.ShouldBe(existing.Id);
        after.Name.ShouldBe("Build");
        after.Rate.ShouldBe(100m);
        after.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void PayloadWithOnlyDeletions_AllActiveBecomeArchived()
    {
        var contract = MakeContract().WithTasks(("A", 1m), ("B", 2m));

        contract.ApplyTasks([], Now);

        contract.Tasks.ShouldAllBe(t => t.DeletedAt == Now);
    }

    [Fact]
    public void Resurrect_DoesNotMatch_ActiveNameClash_OnlyArchived()
    {
        // If an active task has the same name as an empty-Id payload entry (no Id given),
        // we should NOT resurrect — but the validator should reject this; ApplyTasks
        // would create a new task. We don't test the validator here; we just confirm
        // ApplyTasks doesn't pick up an active task as "resurrect candidate".
        var contract = MakeContract().WithTasks(("Build", 100m));
        var existing = contract.Tasks.Single();

        contract.ApplyTasks(
            [
                new UpdateContractTaskDto(existing.Id, "Build", 100m),
                new UpdateContractTaskDto(null, "BUILD", 200m),
            ],
            Now);

        // Two active tasks now (existing kept + new created), because no archived match.
        contract.Tasks.Where(t => t.DeletedAt == null).Count().ShouldBe(2);
    }
}
