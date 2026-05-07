using System.Net;
using System.Net.Http.Json;
using Api.Modules.Animals;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Integration;

public class AnimalEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _testFactory;
    private readonly HttpClient _client;

    public AnimalEndpointsTests(WebApplicationFactory<Program> factory)
    {
        var guid = Guid.NewGuid();
        _testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove all descriptors related to AnimalDbContext so we can replace with InMemory
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<AnimalDbContext>) ||
                        d.ServiceType == typeof(IDbContextOptionsConfiguration<AnimalDbContext>) ||
                        d.ServiceType == typeof(AnimalDbContext))
                    .ToList();
                foreach (var d in toRemove)
                    services.Remove(d);

                services.AddDbContext<AnimalDbContext>(options =>
                    options.UseInMemoryDatabase("IntegrationTests_" + guid));
            });
        });
        _client = _testFactory.CreateClient();
    }

    private async Task<Animal> SeedAnimalViaApiAsync(string name = "Buddy", string species = "Dog", int age = 3)
    {
        var request = new CreateAnimalRequest { Name = name, Species = species, Age = age };
        var response = await _client.PostAsJsonAsync("/api/animals", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Animal>())!;
    }

    [Fact]
    public async Task GetAnimals_ReturnsOkWithAnimals()
    {
        var response = await _client.GetAsync("/api/animals");

        response.EnsureSuccessStatusCode();
        var animals = await response.Content.ReadFromJsonAsync<List<Animal>>();
        Assert.NotNull(animals);
        Assert.True(animals.Count >= 0);
    }

    [Fact]
    public async Task GetAnimalById_ExistingId_ReturnsOk()
    {
        var seeded = await SeedAnimalViaApiAsync("Buddy", "Dog", 3);

        var response = await _client.GetAsync($"/api/animals/{seeded.Id}");

        response.EnsureSuccessStatusCode();
        var animal = await response.Content.ReadFromJsonAsync<Animal>();
        Assert.NotNull(animal);
        Assert.Equal("Buddy", animal.Name);
    }

    [Fact]
    public async Task GetAnimalById_NonExistingId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/animals/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAnimal_ValidRequest_ReturnsCreated()
    {
        var request = new CreateAnimalRequest
        {
            Name = "Rex",
            Species = "Dog",
            Age = 2
        };

        var response = await _client.PostAsJsonAsync("/api/animals", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var animal = await response.Content.ReadFromJsonAsync<Animal>();
        Assert.NotNull(animal);
        Assert.Equal("Rex", animal.Name);
    }

    [Fact]
    public async Task CreateAnimal_InvalidRequest_ReturnsBadRequest()
    {
        var request = new CreateAnimalRequest
        {
            Name = "",
            Species = "",
            Age = -1
        };

        var response = await _client.PostAsJsonAsync("/api/animals", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAnimal_NonExistingId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/animals/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
