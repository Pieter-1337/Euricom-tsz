using Tsz.Api.Modules.Animals;
using Tsz.Infrastructure.Abstractions;
using Moq;
using Shouldly;

namespace Tsz.Api.Tests.Modules.Animals;

public class DeleteAnimalHandlerTests
{
    [Fact]
    public async Task HandleAsync_ExistingAnimal_RemovesAndReturnsTrue()
    {
        var existing = Animal.Create("Rex", "Dog", 2);
        var repo = new Mock<IRepository<Animal>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Animal>()).Returns(repo.Object);

        var handler = new DeleteAnimalHandler(uow.Object);

        var result = await handler.HandleAsync(new DeleteAnimalCommand(existing.Id));

        result.ShouldBeTrue();
        repo.Verify(r => r.Remove(existing), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MissingAnimal_ReturnsFalse()
    {
        var repo = new Mock<IRepository<Animal>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Animal?)null);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Animal>()).Returns(repo.Object);

        var handler = new DeleteAnimalHandler(uow.Object);

        var result = await handler.HandleAsync(new DeleteAnimalCommand(Guid.NewGuid()));

        result.ShouldBeFalse();
        repo.Verify(r => r.Remove(It.IsAny<Animal>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
