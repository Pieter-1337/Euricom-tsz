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
    public async Task HandleAsync_ReturnsAllAsDtos()
    {
        var dtos = new[]
        {
            new UserDto(Guid.NewGuid(), "a@x.com", "A", UserRole.Admin, 20, 5, 0, 0),
            new UserDto(Guid.NewGuid(), "b@x.com", "B", UserRole.User, 20, 5, 0, 0),
        };
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.GetAllAsDtosAsync<UserDto>(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtos);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);

        var handler = new GetUsersHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUsersQuery());

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("A");
        result[1].Role.ShouldBe(UserRole.User);
    }
}
