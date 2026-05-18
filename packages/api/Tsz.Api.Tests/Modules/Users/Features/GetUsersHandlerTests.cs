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
            new UserDto(Guid.NewGuid(), "a@x.com", "A", "Alpha", UserRole.Admin),
            new UserDto(Guid.NewGuid(), "b@x.com", "B", "Beta", UserRole.User),
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
        result[1].Role.ShouldBe(UserRole.User);
    }
}
