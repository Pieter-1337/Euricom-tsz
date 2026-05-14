using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class DeleteUserLeaveValidatorTests
{
    private static DeleteUserLeaveValidator BuildValidator(bool rowExists = true)
    {
        var ulRepo = new Mock<IRepository<UserLeave>>();
        ulRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<UserLeave, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rowExists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(ulRepo.Object);

        return new DeleteUserLeaveValidator(uow.Object);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new DeleteUserLeaveCommand(Guid.NewGuid(), Guid.NewGuid()));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyUserId_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new DeleteUserLeaveCommand(Guid.Empty, Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteUserLeaveCommand.UserId));
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new DeleteUserLeaveCommand(Guid.NewGuid(), Guid.Empty));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteUserLeaveCommand.Id));
    }

    [Fact]
    public async Task RowNotFound_Fails_WithNotFoundError()
    {
        var result = await BuildValidator(rowExists: false).ValidateAsync(new DeleteUserLeaveCommand(Guid.NewGuid(), Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == UserLeaveErrors.NotFound.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }
}
