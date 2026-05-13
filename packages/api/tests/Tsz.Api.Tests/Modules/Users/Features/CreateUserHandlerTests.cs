using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class CreateUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_NewEmail_AddsUserWithDefaultLeaves_AndReturnsDto()
    {
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);

        var handler = new CreateUserHandler(uow.Object);

        var result = await handler.HandleAsync(new CreateUserCommand("Jane", "jane@example.com", UserRole.User));

        result.Conflict.ShouldBeFalse();
        result.User.ShouldNotBeNull();
        result.User!.Name.ShouldBe("Jane");
        result.User.Email.ShouldBe("jane@example.com");
        result.User.Role.ShouldBe(UserRole.User);
        result.User.HolidayDays.ShouldBe(User.DefaultHolidayDays);
        result.User.AdvDays.ShouldBe(User.DefaultAdvDays);
        result.User.AncienniteitDays.ShouldBe(User.DefaultAncienniteitDays);
        result.User.SicknessDays.ShouldBe(User.DefaultSicknessDays);

        repo.Verify(r => r.Add(It.Is<User>(u =>
            u.Name == "Jane" && u.Email == "jane@example.com" && u.Role == UserRole.User)), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DuplicateEmail_ReturnsConflict_AndDoesNotPersist()
    {
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);

        var handler = new CreateUserHandler(uow.Object);

        var result = await handler.HandleAsync(new CreateUserCommand("Jane", "jane@example.com", UserRole.User));

        result.Conflict.ShouldBeTrue();
        result.User.ShouldBeNull();
        repo.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
