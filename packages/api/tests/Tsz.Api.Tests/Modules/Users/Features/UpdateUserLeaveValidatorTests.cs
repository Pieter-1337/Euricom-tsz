using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class UpdateUserLeaveValidatorTests
{
    private static UpdateUserLeaveValidator BuildValidator(
        bool rowExists = true,
        LeaveAllowed allowed = LeaveAllowed.Limited,
        decimal? entityDays = 10m)
    {
        var leaveTypeId = Guid.NewGuid();
        var lt = LeaveType.Create("Verlof", allowed, allowed == LeaveAllowed.Limited ? 20m : null);
        lt.Id = leaveTypeId;

        var userLeaveRepo = new Mock<IRepository<UserLeave>>();
        userLeaveRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<UserLeave, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rowExists);

        var userLeave = UserLeave.Create(Guid.NewGuid(), leaveTypeId, entityDays);
        userLeaveRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserLeave, bool>>>(), It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(rowExists ? userLeave : null);

        var leaveTypeRepo = new Mock<IRepository<LeaveType>>();
        leaveTypeRepo
            .Setup(r => r.GetByIdAsync(leaveTypeId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(lt);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(userLeaveRepo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(leaveTypeRepo.Object);

        return new UpdateUserLeaveValidator(uow.Object);
    }

    [Fact]
    public async Task Valid_WithDays_Passes()
    {
        var validator = BuildValidator(rowExists: true, allowed: LeaveAllowed.Limited);
        var result = await validator.ValidateAsync(new UpdateUserLeaveCommand(Guid.NewGuid(), Guid.NewGuid(), 10m));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Valid_NullDays_Unlimited_Passes()
    {
        var validator = BuildValidator(rowExists: true, allowed: LeaveAllowed.Unlimited);
        var result = await validator.ValidateAsync(new UpdateUserLeaveCommand(Guid.NewGuid(), Guid.NewGuid(), null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task NegativeDays_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserLeaveCommand(Guid.NewGuid(), Guid.NewGuid(), -1m));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserLeaveCommand.TotalDays));
    }

    [Fact]
    public async Task EmptyUserId_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserLeaveCommand(Guid.Empty, Guid.NewGuid(), 5m));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserLeaveCommand.UserId));
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserLeaveCommand(Guid.NewGuid(), Guid.Empty, 5m));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserLeaveCommand.Id));
    }

    [Fact]
    public async Task RowNotFound_Fails_WithNotFoundError()
    {
        var validator = BuildValidator(rowExists: false);
        var result = await validator.ValidateAsync(new UpdateUserLeaveCommand(Guid.NewGuid(), Guid.NewGuid(), 5m));
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == UserLeaveErrors.NotFound.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }

    [Fact]
    public async Task UnlimitedType_WithDays_Fails_WithInvalidErrorCode()
    {
        var validator = BuildValidator(rowExists: true, allowed: LeaveAllowed.Unlimited, entityDays: null);
        var result = await validator.ValidateAsync(new UpdateUserLeaveCommand(Guid.NewGuid(), Guid.NewGuid(), 5m));
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == CommonErrors.Invalid.Code);
        error.CustomState.ShouldBe(CommonErrors.Invalid);
        ((IErrorCode)error.CustomState).Category.ShouldBe(ErrorCategory.Validation);
    }
}
