using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.LeaveTypes.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.LeaveTypes.Features;

public class UpdateLeaveTypeValidatorTests
{
    private static UpdateLeaveTypeValidator BuildValidator(
        bool leaveTypeExists = true,
        bool nameConflict = false)
    {
        var repo = new Mock<IRepository<LeaveType>>();

        // The validator calls ExistsAsync twice: once for id existence, once for name uniqueness.
        // FluentValidation runs rules sequentially. Use a sequence to return different values.
        var seq = repo.SetupSequence(r => r.ExistsAsync(
            It.IsAny<Expression<Func<LeaveType, bool>>>(),
            It.IsAny<CancellationToken>()));

        seq.ReturnsAsync(leaveTypeExists)   // id existence check
           .ReturnsAsync(nameConflict);     // name conflict check (only reached if id check passes)

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(repo.Object);

        return new UpdateLeaveTypeValidator(uow.Object);
    }

    private static UpdateLeaveTypeCommand ValidCommand(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "Verlof", LeaveAllowed.Limited, 20m, null, null, null, null);

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCommand(Guid.Empty));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateLeaveTypeCommand.Id));
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var validator = BuildValidator();
        var cmd = ValidCommand() with { Name = "" };
        var result = await validator.ValidateAsync(cmd);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateLeaveTypeCommand.Name));
    }

    [Fact]
    public async Task LeaveTypeNotFound_Fails_WithNotFoundError()
    {
        var result = await BuildValidator(leaveTypeExists: false).ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == LeaveTypeErrors.NotFound.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }

    [Fact]
    public async Task NameConflict_Fails_WithNameAlreadyExistsError()
    {
        var result = await BuildValidator(leaveTypeExists: true, nameConflict: true).ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == LeaveTypeErrors.NameAlreadyExists.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.Conflict);
    }
}
