using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class GetUsersHandlerTests
{
    [Fact]
    public async Task HandleAsync_NoFilter_ReturnsAllAsDtos()
    {
        var dtos = new[]
        {
            new UserDto(Guid.NewGuid(), "a@x.com", "A", "Alpha", [UserRole.Admin]),
            new UserDto(Guid.NewGuid(), "b@x.com", "B", "Beta", [UserRole.User]),
        };
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.GetAllAsDtosAsync<UserDto>(null, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(dtos);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);

        var handler = new GetUsersHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUsersQuery());

        result.Count.ShouldBe(2);
        result[0].FirstName.ShouldBe("A");
        result[1].Roles.ShouldBe([UserRole.User]);
    }

    [Fact]
    public async Task HandleAsync_WithRoleFilter_PassesFilterToRepo()
    {
        var managers = new[]
        {
            new UserDto(Guid.NewGuid(), "cm@x.com", "Cam", "Manager", [UserRole.ClientManager]),
        };
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.GetAllAsDtosAsync<UserDto>(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(managers);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);

        var handler = new GetUsersHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUsersQuery(UserRole.ClientManager));

        result.Count.ShouldBe(1);
        result[0].Roles.ShouldContain(UserRole.ClientManager);
        repo.Verify(r => r.GetAllAsDtosAsync<UserDto>(
            It.Is<Expression<Func<User, bool>>>(e => e != null),
            It.IsAny<CancellationToken>(),
            false), Times.Once);
    }
}
