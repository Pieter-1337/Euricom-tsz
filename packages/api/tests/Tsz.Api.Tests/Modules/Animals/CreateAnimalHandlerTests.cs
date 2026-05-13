using Tsz.Api.Modules.Animals;
using Tsz.Infrastructure.Abstractions;
using Moq;
using Shouldly;

namespace Tsz.Api.Tests.Modules.Animals;

public class CreateAnimalHandlerTests
{
    [Fact]
    public async Task HandleAsync_AddsAnimalAndReturnsDto()
    {
        var repo = new Mock<IRepository<Animal>>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Animal>()).Returns(repo.Object);

        var handler = new CreateAnimalHandler(uow.Object);

        var result = await handler.HandleAsync(new CreateAnimalCommand("Rex", "Dog", 2));

        result.Name.ShouldBe("Rex");
        result.Species.ShouldBe("Dog");
        result.Age.ShouldBe(2);
        result.Id.ShouldNotBe(Guid.Empty);

        repo.Verify(r => r.Add(It.Is<Animal>(a =>
            a.Name == "Rex" && a.Species == "Dog" && a.Age == 2)), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
