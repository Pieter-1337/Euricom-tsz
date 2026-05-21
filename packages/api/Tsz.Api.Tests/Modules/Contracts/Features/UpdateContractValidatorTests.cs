using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class UpdateContractValidatorTests
{
    private static UpdateContractValidator BuildValidator(
        bool contractExists = true,
        bool userIsClientManager = true,
        Contract? loadedContract = null,
        HashSet<Guid>? existingUserIds = null)
    {
        var repo = new Mock<IRepository<Contract>>();
        repo.Setup(r => r.ExistsAsync(
                It.IsAny<Expression<Func<Contract, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(contractExists);
        repo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Contract, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(loadedContract);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Contract>()).Returns(repo.Object);

        var users = new Mock<IUsersAccessModule>();
        users
            .Setup(m => m.ExecuteQueryAsync(It.IsAny<UserExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserExistsQuery q, CancellationToken _) =>
                existingUserIds is null || existingUserIds.Contains(q.UserId));
        users
            .Setup(m => m.ExecuteQueryAsync(It.IsAny<UserHasRoleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userIsClientManager);

        return new UpdateContractValidator(uow.Object, users.Object);
    }

    private static UpdateContractCommand ValidCmd(
        Guid? id = null,
        string? subject = "Engagement Alpha",
        DateOnly? start = null,
        DateOnly? end = null,
        Guid? clientManagerId = null,
        IReadOnlyList<Guid>? consultantIds = null,
        IReadOnlyList<UpdateContractTaskDto>? tasks = null) =>
        new(
            id ?? Guid.NewGuid(),
            subject ?? "Engagement Alpha",
            clientManagerId ?? Guid.NewGuid(),
            start ?? new DateOnly(2026, 1, 1),
            end,
            consultantIds ?? [],
            tasks);

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(id: Guid.Empty));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateContractCommand.Id));
    }

    [Fact]
    public async Task EmptySubject_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(subject: ""));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateContractCommand.Subject));
    }

    [Fact]
    public async Task SubjectTooLong_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(subject: new string('x', 257)));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateContractCommand.Subject));
    }

    [Fact]
    public async Task EndBeforeStart_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            start: new DateOnly(2026, 6, 1),
            end: new DateOnly(2026, 5, 31)));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.EndBeforeStart.Code);
    }

    [Fact]
    public async Task EndEqualToStart_Passes()
    {
        var date = new DateOnly(2026, 6, 1);
        var result = await BuildValidator().ValidateAsync(ValidCmd(start: date, end: date));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task NullEnd_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(start: new DateOnly(2026, 1, 1), end: null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ClientManagerNotFound_Fails()
    {
        var validator = BuildValidator(userIsClientManager: false, existingUserIds: []);
        var result = await validator.ValidateAsync(ValidCmd());
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.ClientManagerNotFound.Code);
    }

    [Fact]
    public async Task ClientManagerMissingRole_Fails()
    {
        var result = await BuildValidator(userIsClientManager: false).ValidateAsync(ValidCmd());
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.ClientManagerMissingRole.Code);
    }

    [Fact]
    public async Task NullClientManager_Passes()
    {
        var cmd = ValidCmd() with { ClientManagerId = null };
        var result = await BuildValidator(userIsClientManager: false, existingUserIds: []).ValidateAsync(cmd);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ContractNotFound_Fails()
    {
        var result = await BuildValidator(contractExists: false).ValidateAsync(ValidCmd());
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.NotFound.Code);
    }

    [Fact]
    public async Task ConsultantDuplicates_Fail()
    {
        var dup = Guid.NewGuid();
        var result = await BuildValidator().ValidateAsync(ValidCmd(consultantIds: [dup, dup]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.ConsultantDuplicates.Code);
    }

    [Fact]
    public async Task NewConsultantNotFound_Fails()
    {
        var contractId = Guid.NewGuid();
        var loaded = ContractBuilder.Build(1).WithId(contractId);
        var validator = BuildValidator(loadedContract: loaded, existingUserIds: []);

        var result = await validator.ValidateAsync(ValidCmd(
            id: contractId,
            consultantIds: [Guid.NewGuid()]));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.ConsultantNotFound.Code);
    }

    [Fact]
    public async Task ExistingConsultantNotRevalidated()
    {
        var contractId = Guid.NewGuid();
        var existingConsultantId = Guid.NewGuid();
        var loaded = ContractBuilder.Build(1).WithId(contractId).WithConsultants(existingConsultantId);

        // existing user set excludes the consultant (simulates user is soft-deleted)
        var validator = BuildValidator(loadedContract: loaded, existingUserIds: []);

        var result = await validator.ValidateAsync(ValidCmd(
            id: contractId,
            consultantIds: [existingConsultantId]));

        result.Errors.ShouldNotContain(e => e.ErrorCode == ContractErrors.ConsultantNotFound.Code);
    }

    [Fact]
    public async Task Tasks_Null_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(tasks: null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Tasks_EmptyName_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            tasks: [new UpdateContractTaskDto(null, "", 10m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.TaskNameRequired.Code);
    }

    [Fact]
    public async Task Tasks_NameTooLong_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            tasks: [new UpdateContractTaskDto(null, new string('x', 257), 10m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.TaskNameTooLong.Code);
    }

    [Fact]
    public async Task Tasks_NonPositiveRate_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            tasks: [new UpdateContractTaskDto(null, "Build", 0m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.TaskRateNonPositive.Code);
    }

    [Fact]
    public async Task Tasks_DuplicateNames_CaseInsensitive_Fail()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(tasks: [
            new UpdateContractTaskDto(null, "Build", 10m),
            new UpdateContractTaskDto(null, "build", 20m),
        ]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.DuplicateTaskName.Code);
    }

    [Fact]
    public async Task Tasks_UnknownPayloadId_Fails()
    {
        var contractId = Guid.NewGuid();
        var loaded = ContractBuilder.Build(1).WithId(contractId);
        var validator = BuildValidator(loadedContract: loaded);

        var result = await validator.ValidateAsync(ValidCmd(
            id: contractId,
            tasks: [new UpdateContractTaskDto(Guid.NewGuid(), "Build", 10m)]));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.TaskNotFound.Code);
    }

    [Fact]
    public async Task Tasks_KnownPayloadId_Passes()
    {
        var contractId = Guid.NewGuid();
        var loaded = ContractBuilder.Build(1).WithId(contractId)
            .WithTasks(("Build", 100m));
        var taskId = loaded.Tasks.Single().Id;
        var validator = BuildValidator(loadedContract: loaded);

        var result = await validator.ValidateAsync(ValidCmd(
            id: contractId,
            tasks: [new UpdateContractTaskDto(taskId, "Build v2", 110m)]));

        result.IsValid.ShouldBeTrue();
    }
}
