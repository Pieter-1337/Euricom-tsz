using Api.Modules.Animals;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public class AnimalServiceTests
{
    private static AnimalService CreateService(out AnimalDbContext context)
    {
        var options = new DbContextOptionsBuilder<AnimalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        context = new AnimalDbContext(options);
        return new AnimalService(context);
    }

    [Fact]
    public async Task GetAll_ReturnsAllAnimals()
    {
        var service = CreateService(out var context);
        await context.Animals.AddRangeAsync(
            new Animal { Name = "Buddy", Species = "Dog", Age = 3 },
            new Animal { Name = "Whiskers", Species = "Cat", Age = 5 }
        );
        await context.SaveChangesAsync();

        var animals = await service.GetAllAsync();

        Assert.Equal(2, animals.Count);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsAnimal()
    {
        var service = CreateService(out var context);
        var added = context.Animals.Add(new Animal { Name = "Buddy", Species = "Dog", Age = 3 });
        await context.SaveChangesAsync();

        var animal = await service.GetByIdAsync(added.Entity.Id);

        Assert.NotNull(animal);
        Assert.Equal("Buddy", animal.Name);
    }

    [Fact]
    public async Task GetById_NonExistingId_ReturnsNull()
    {
        var service = CreateService(out _);

        var animal = await service.GetByIdAsync(999);

        Assert.Null(animal);
    }

    [Fact]
    public async Task Create_AddsAnimalAndReturnsIt()
    {
        var service = CreateService(out _);
        var request = new CreateAnimalRequest
        {
            Name = "Rex",
            Species = "Dog",
            Age = 2
        };

        var created = await service.CreateAsync(request);

        Assert.Equal("Rex", created.Name);
        Assert.Equal("Dog", created.Species);
        Assert.Equal(2, created.Age);
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsTrueAndRemoves()
    {
        var service = CreateService(out var context);
        var added = context.Animals.Add(new Animal { Name = "Buddy", Species = "Dog", Age = 3 });
        await context.SaveChangesAsync();

        var result = await service.DeleteAsync(added.Entity.Id);
        var animals = await service.GetAllAsync();

        Assert.True(result);
        Assert.Empty(animals);
        Assert.Null(await service.GetByIdAsync(added.Entity.Id));
    }

    [Fact]
    public async Task Delete_NonExistingId_ReturnsFalse()
    {
        var service = CreateService(out _);

        var result = await service.DeleteAsync(999);

        Assert.False(result);
    }
}
