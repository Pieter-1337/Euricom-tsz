using Tsz.Api.Modules.Animals;
using Tsz.Infrastructure.Abstractions;
using Moq;
using Shouldly;

namespace Tsz.Api.Tests.Modules.Animals;

public class GetAnimalsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsAllAsDtos()
    {
        var dtos = new[]
        {
            new AnimalDto(Guid.NewGuid(), "A", "Dog", 1),
            new AnimalDto(Guid.NewGuid(), "B", "Cat", 2),
        };
        var repo = new Mock<IRepository<Animal>>();
        repo.Setup(r => r.GetAllAsDtosAsync<AnimalDto>(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtos);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Animal>()).Returns(repo.Object);

        var handler = new GetAnimalsHandler(uow.Object);

        var result = await handler.HandleAsync(new GetAnimalsQuery());

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("A");
        result[1].Species.ShouldBe("Cat");
    }
}
