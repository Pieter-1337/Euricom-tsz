using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.LeaveTypes.Features;
using Tsz.Api.Modules.Users;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.LeaveTypes.Features;

public class DeleteLeaveTypeValidatorTests
{
    private static DeleteLeaveTypeValidator BuildValidator(
        bool leaveTypeExists = true,
        bool inUse = false)
    {
        var ltRepo = new Mock<IRepository<LeaveType>>();
        ltRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<LeaveType, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveTypeExists);

        var ulRepo = new Mock<IRepository<UserLeave>>();
        ulRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<UserLeave, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inUse);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(ltRepo.Object);
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(ulRepo.Object);

        return new DeleteLeaveTypeValidator(uow.Object);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new DeleteLeaveTypeCommand(Guid.NewGuid()));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new DeleteLeaveTypeCommand(Guid.Empty));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteLeaveTypeCommand.Id));
    }

    [Fact]
    public async Task LeaveTypeNotFound_Fails_WithNotFoundError()
    {
        var result = await BuildValidator(leaveTypeExists: false).ValidateAsync(new DeleteLeaveTypeCommand(Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == LeaveTypeErrors.NotFound.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }

    [Fact]
    public async Task InUse_Fails_WithInUseError()
    {
        var result = await BuildValidator(leaveTypeExists: true, inUse: true).ValidateAsync(new DeleteLeaveTypeCommand(Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == LeaveTypeErrors.InUse.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.Conflict);
    }
}
