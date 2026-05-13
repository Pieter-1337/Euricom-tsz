using System.Net;
using System.Net.Http.Json;
using Tsz.Api.Modules.Animals;
using Tsz.Api.Tests.Integration.TestAuth;

namespace Tsz.Api.Tests.Integration;

public class AnimalEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AnimalEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<AnimalDto> SeedAnimalViaApiAsync(string name = "Buddy", string species = "Dog", int age = 3)
    {
        var command = new CreateAnimalCommand(name, species, age);
        var response = await _client.PostAsJsonAsync("/api/animals", command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AnimalDto>())!;
    }

    [Fact]
    public async Task GetAnimals_ReturnsOkWithAnimals()
    {
        var response = await _client.GetAsync("/api/animals");

        response.EnsureSuccessStatusCode();
        var animals = await response.Content.ReadFromJsonAsync<List<AnimalDto>>();
        Assert.NotNull(animals);
        Assert.True(animals.Count >= 0);
    }

    [Fact]
    public async Task GetAnimalById_ExistingId_ReturnsOk()
    {
        var seeded = await SeedAnimalViaApiAsync("Buddy", "Dog", 3);

        var response = await _client.GetAsync($"/api/animals/{seeded.Id}");

        response.EnsureSuccessStatusCode();
        var animal = await response.Content.ReadFromJsonAsync<AnimalDto>();
        Assert.NotNull(animal);
        Assert.Equal("Buddy", animal.Name);
    }

    [Fact]
    public async Task GetAnimalById_NonExistingId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/animals/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAnimal_ValidRequest_ReturnsCreated()
    {
        var command = new CreateAnimalCommand("Rex", "Dog", 2);

        var response = await _client.PostAsJsonAsync("/api/animals", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var animal = await response.Content.ReadFromJsonAsync<AnimalDto>();
        Assert.NotNull(animal);
        Assert.Equal("Rex", animal.Name);
    }

    [Fact]
    public async Task CreateAnimal_InvalidRequest_ReturnsBadRequest()
    {
        var command = new CreateAnimalCommand("", "", -1);

        var response = await _client.PostAsJsonAsync("/api/animals", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAnimal_ExistingId_ReturnsOk()
    {
        var seeded = await SeedAnimalViaApiAsync("Old", "Dog", 1);
        var command = new UpdateAnimalCommand(seeded.Id, "New", "Cat", 5);

        var response = await _client.PutAsJsonAsync($"/api/animals/{seeded.Id}", command);

        response.EnsureSuccessStatusCode();
        var animal = await response.Content.ReadFromJsonAsync<AnimalDto>();
        Assert.NotNull(animal);
        Assert.Equal("New", animal.Name);
        Assert.Equal("Cat", animal.Species);
        Assert.Equal(5, animal.Age);
    }

    [Fact]
    public async Task UpdateAnimal_NonExistingId_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var command = new UpdateAnimalCommand(id, "X", "Y", 1);

        var response = await _client.PutAsJsonAsync($"/api/animals/{id}", command);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAnimal_ExistingId_ReturnsNoContent()
    {
        var seeded = await SeedAnimalViaApiAsync();

        var response = await _client.DeleteAsync($"/api/animals/{seeded.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAnimal_NonExistingId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/animals/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
