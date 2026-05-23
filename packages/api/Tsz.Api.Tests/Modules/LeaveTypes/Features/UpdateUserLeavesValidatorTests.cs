using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Modules.LeaveTypes.Domain.LeaveTypes;
using Tsz.Modules.LeaveTypes.Domain.Leaves;
using Tsz.Modules.LeaveTypes.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.LeaveTypes.Features;

public class UpdateUserLeavesValidatorTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private const int ValidYear = 2026;

    private static UpdateUserLeavesValidator BuildValidator(
        IEnumerable<UserLeave>? dbRows = null,
        IEnumerable<LeaveType>? leaveTypes = null)
    {
        var rows = (dbRows ?? []).ToList();
        var lts = (leaveTypes ?? []).ToList();

        var userLeaveRepo = new Mock<IRepository<UserLeave>>();
        userLeaveRepo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<UserLeave, bool>>>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(rows);

        var ltRepo = new Mock<IRepository<LeaveType>>();
        ltRepo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<LeaveType, bool>>>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(lts);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(userLeaveRepo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(ltRepo.Object);

        return new UpdateUserLeavesValidator(uow.Object);
    }

    [Fact]
    public async Task EmptyUserId_Fails_WithRequired()
    {
        var leaveId = Guid.NewGuid();
        var validator = BuildValidator();
        var cmd = new UpdateUserLeavesCommand(Guid.Empty, ValidYear, [new UpdateUserLeavesItem(leaveId, 10m)]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(UpdateUserLeavesCommand.UserId) &&
            e.ErrorCode == CommonErrors.Required.Code);
    }

    [Fact]
    public async Task YearOutOfRange_Fails_WithInvalid()
    {
        var validator = BuildValidator();
        var leaveId = Guid.NewGuid();
        var cmd = new UpdateUserLeavesCommand(_userId, 1999, [new UpdateUserLeavesItem(leaveId, 10m)]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(UpdateUserLeavesCommand.Year) &&
            e.ErrorCode == CommonErrors.Invalid.Code);
    }

    [Fact]
    public async Task EmptyItems_Fails_WithRequired()
    {
        var validator = BuildValidator();
        var cmd = new UpdateUserLeavesCommand(_userId, ValidYear, []);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(UpdateUserLeavesCommand.Items) &&
            e.ErrorCode == CommonErrors.Required.Code);
    }

    [Fact]
    public async Task DuplicateItemIds_Fails_WithInvalid()
    {
        var validator = BuildValidator();
        var duplicateId = Guid.NewGuid();
        var cmd = new UpdateUserLeavesCommand(_userId, ValidYear,
        [
            new UpdateUserLeavesItem(duplicateId, 10m),
            new UpdateUserLeavesItem(duplicateId, 5m),
        ]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(UpdateUserLeavesCommand.Items) &&
            e.ErrorCode == CommonErrors.Invalid.Code);
    }

    [Fact]
    public async Task EmptyItemId_Fails_WithRequired()
    {
        var lt = LeaveTypeBuilder.Limited("Verlof", 20m);
        var row = UserLeaveBuilder.Build(_userId, lt.Id, ValidYear, 20m);
        var validator = BuildValidator([row], [lt]);
        var cmd = new UpdateUserLeavesCommand(_userId, ValidYear,
            [new UpdateUserLeavesItem(Guid.Empty, 10m)]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == CommonErrors.Required.Code);
    }

    [Fact]
    public async Task NegativeTotalDays_Fails_WithInvalid()
    {
        var lt = LeaveTypeBuilder.Limited("Verlof", 20m);
        var row = UserLeaveBuilder.Build(_userId, lt.Id, ValidYear, 20m);
        var validator = BuildValidator([row], [lt]);
        var cmd = new UpdateUserLeavesCommand(_userId, ValidYear,
            [new UpdateUserLeavesItem(row.Id, -1m)]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == CommonErrors.Invalid.Code);
    }

    [Fact]
    public async Task ItemIdNotFound_Fails_WithNotFound()
    {
        var validator = BuildValidator(dbRows: []);
        var unknownId = Guid.NewGuid();
        var cmd = new UpdateUserLeavesCommand(_userId, ValidYear,
            [new UpdateUserLeavesItem(unknownId, 10m)]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.ErrorCode == UserLeaveErrors.NotFound.Code);
        (result.Errors.First(e => e.ErrorCode == UserLeaveErrors.NotFound.Code)
            .CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }

    [Fact]
    public async Task LimitedType_NullTotalDays_Fails_WithMismatch()
    {
        var lt = LeaveTypeBuilder.Limited("Verlof", 20m);
        var row = UserLeaveBuilder.Build(_userId, lt.Id, ValidYear, 20m);
        var validator = BuildValidator([row], [lt]);
        var cmd = new UpdateUserLeavesCommand(_userId, ValidYear,
            [new UpdateUserLeavesItem(row.Id, null)]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == UserLeaveErrors.TotalDaysAllowedMismatch.Code);
    }

    [Fact]
    public async Task UnlimitedType_NonNullTotalDays_Fails_WithMismatch()
    {
        var lt = LeaveTypeBuilder.Unlimited("Ziekte");
        var row = UserLeaveBuilder.Build(_userId, lt.Id, ValidYear, null);
        var validator = BuildValidator([row], [lt]);
        var cmd = new UpdateUserLeavesCommand(_userId, ValidYear,
            [new UpdateUserLeavesItem(row.Id, 5m)]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == UserLeaveErrors.TotalDaysAllowedMismatch.Code);
    }

    [Fact]
    public async Task HappyPath_LimitedWithDays_Passes()
    {
        var lt = LeaveTypeBuilder.Limited("Verlof", 20m);
        var row = UserLeaveBuilder.Build(_userId, lt.Id, ValidYear, 20m);
        var validator = BuildValidator([row], [lt]);
        var cmd = new UpdateUserLeavesCommand(_userId, ValidYear,
            [new UpdateUserLeavesItem(row.Id, 22m)]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task HappyPath_UnlimitedWithNullDays_Passes()
    {
        var lt = LeaveTypeBuilder.Unlimited("Ziekte");
        var row = UserLeaveBuilder.Build(_userId, lt.Id, ValidYear, null);
        var validator = BuildValidator([row], [lt]);
        var cmd = new UpdateUserLeavesCommand(_userId, ValidYear,
            [new UpdateUserLeavesItem(row.Id, null)]);

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.ShouldBeTrue();
    }
}
