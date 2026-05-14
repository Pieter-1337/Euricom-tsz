using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.LeaveTypes.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.LeaveTypes.Features;

public class CreateLeaveTypeValidatorTests
{
    private static CreateLeaveTypeValidator BuildValidator(bool nameExists = false)
    {
        var repo = new Mock<IRepository<LeaveType>>();
        repo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<LeaveType, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nameExists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(repo.Object);

        return new CreateLeaveTypeValidator(uow.Object);
    }

    private static CreateLeaveTypeCommand ValidCommand(string name = "Verlof") =>
        new(name, LeaveAllowed.Limited, 20m, null, null, null, null);

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCommand(""));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateLeaveTypeCommand.Name));
    }

    [Fact]
    public async Task NameAlreadyExists_Fails_WithConflictError()
    {
        var result = await BuildValidator(nameExists: true).ValidateAsync(ValidCommand("Verlof"));
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == LeaveTypeErrors.NameAlreadyExists.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.Conflict);
    }
}
