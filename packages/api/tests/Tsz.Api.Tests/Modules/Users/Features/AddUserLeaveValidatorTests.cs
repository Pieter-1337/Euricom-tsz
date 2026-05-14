using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class AddUserLeaveValidatorTests
{
    private static AddUserLeaveValidator BuildValidator(
        bool userExists = true,
        bool leaveTypeExists = true,
        bool duplicate = false)
    {
        var userRepo = new Mock<IRepository<User>>();
        userRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userExists);

        var ltRepo = new Mock<IRepository<LeaveType>>();
        ltRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<LeaveType, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveTypeExists);

        var ulRepo = new Mock<IRepository<UserLeave>>();
        ulRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<UserLeave, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicate);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(ltRepo.Object);
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(ulRepo.Object);

        return new AddUserLeaveValidator(uow.Object);
    }

    private static AddUserLeaveCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 10m);

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyUserId_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new AddUserLeaveCommand(Guid.Empty, Guid.NewGuid(), 5m));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(AddUserLeaveCommand.UserId));
    }

    [Fact]
    public async Task EmptyLeaveTypeId_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new AddUserLeaveCommand(Guid.NewGuid(), Guid.Empty, 5m));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(AddUserLeaveCommand.LeaveTypeId));
    }

    [Fact]
    public async Task UserNotFound_Fails_WithUserNotFoundError()
    {
        var result = await BuildValidator(userExists: false).ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == UserLeaveErrors.UserNotFound.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }

    [Fact]
    public async Task LeaveTypeNotFound_Fails_WithLeaveTypeNotFoundError()
    {
        var result = await BuildValidator(leaveTypeExists: false).ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == UserLeaveErrors.LeaveTypeNotFound.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }

    [Fact]
    public async Task Duplicate_Fails_WithDuplicateError()
    {
        var result = await BuildValidator(duplicate: true).ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == UserLeaveErrors.Duplicate.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.Conflict);
    }
}
