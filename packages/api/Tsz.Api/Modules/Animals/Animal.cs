using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Animals;

public class Animal : IEntityBase
{
    public Guid Id { get; set; }
    public string Name { get; private set; } = string.Empty;
    public string Species { get; private set; } = string.Empty;
    public int Age { get; private set; }

    private Animal() { }

    public static Animal Create(string name, string species, int age) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Species = species,
        Age = age,
    };

    public void Rename(string name) => Name = name;
    public void Reclassify(string species) => Species = species;
    public void ChangeAge(int age) => Age = age;
}
