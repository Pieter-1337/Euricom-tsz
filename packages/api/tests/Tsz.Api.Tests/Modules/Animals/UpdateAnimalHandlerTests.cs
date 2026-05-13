using Tsz.Api.Modules.Animals;
using Tsz.Infrastructure.Abstractions;
using Moq;
using Shouldly;

namespace Tsz.Api.Tests.Modules.Animals;

public class UpdateAnimalHandlerTests
{
    [Fact]
    public async Task HandleAsync_ExistingAnimal_UpdatesAndReturnsDto()
    {
        var existing = Animal.Create("Old", "Dog", 1);
        var repo = new Mock<IRepository<Animal>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Animal>()).Returns(repo.Object);

        var handler = new UpdateAnimalHandler(uow.Object);

        var result = await handler.HandleAsync(new UpdateAnimalCommand(existing.Id, "New", "Cat", 5));

        result.ShouldNotBeNull();
        result.Name.ShouldBe("New");
        result.Species.ShouldBe("Cat");
        result.Age.ShouldBe(5);
        existing.Name.ShouldBe("New");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MissingAnimal_ReturnsNull()
    {
        var repo = new Mock<IRepository<Animal>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Animal?)null);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Animal>()).Returns(repo.Object);

        var handler = new UpdateAnimalHandler(uow.Object);

        var result = await handler.HandleAsync(new UpdateAnimalCommand(Guid.NewGuid(), "x", "y", 1));

        result.ShouldBeNull();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
