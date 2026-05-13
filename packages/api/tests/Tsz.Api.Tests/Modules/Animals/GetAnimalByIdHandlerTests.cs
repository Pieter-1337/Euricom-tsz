using Tsz.Api.Modules.Animals;
using Tsz.Infrastructure.Abstractions;
using Moq;
using Shouldly;

namespace Tsz.Api.Tests.Modules.Animals;

public class GetAnimalByIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_Existing_ReturnsDto()
    {
        var id = Guid.NewGuid();
        var dto = new AnimalDto(id, "Buddy", "Dog", 3);
        var repo = new Mock<IRepository<Animal>>();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<AnimalDto>(
                It.IsAny<System.Linq.Expressions.Expression<Func<Animal, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Animal>()).Returns(repo.Object);

        var handler = new GetAnimalByIdHandler(uow.Object);

        var result = await handler.HandleAsync(new GetAnimalByIdQuery(id));

        result.ShouldBe(dto);
    }

    [Fact]
    public async Task HandleAsync_Missing_ReturnsNull()
    {
        var repo = new Mock<IRepository<Animal>>();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<AnimalDto>(
                It.IsAny<System.Linq.Expressions.Expression<Func<Animal, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnimalDto?)null);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Animal>()).Returns(repo.Object);

        var handler = new GetAnimalByIdHandler(uow.Object);

        var result = await handler.HandleAsync(new GetAnimalByIdQuery(Guid.NewGuid()));

        result.ShouldBeNull();
    }
}
