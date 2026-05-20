using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;
using Tsz.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class GetUserByIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_Existing_ReturnsDto()
    {
        var id = Guid.NewGuid();
        var dto = new UserDto(id, "u@x.com", "First", "Last", [UserRole.User]);
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<UserDto>(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);

        var handler = new GetUserByIdHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUserByIdQuery(id));

        result.ShouldBe(dto);
    }

    [Fact]
    public async Task HandleAsync_Missing_ReturnsNull()
    {
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<UserDto>(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDto?)null);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);

        var handler = new GetUserByIdHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUserByIdQuery(Guid.NewGuid()));

        result.ShouldBeNull();
    }
}
