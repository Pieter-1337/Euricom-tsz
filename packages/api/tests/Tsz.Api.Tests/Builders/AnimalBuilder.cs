using FizzWare.NBuilder;
using FizzWare.NBuilder.Generators;
using Tsz.Api.Modules.Animals;

namespace Tsz.Api.Tests.Builders;

public static class AnimalBuilder
{
    private static readonly string[] DefaultSpecies = ["Dog", "Cat", "Hamster", "Parrot", "Rabbit"];

    public static Animal Build()
    {
        var id = Guid.NewGuid();
        var animal = Animal.Create(
            name: "Name_" + id.ToString()[..8],
            species: Pick<string>.RandomItemFrom(DefaultSpecies),
            age: GetRandom.Int(1, 20));
        animal.Id = id;
        return animal;
    }

    public static Animal WithId(this Animal entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }

    public static Animal WithName(this Animal entity, string name)
    {
        entity.Rename(name);
        return entity;
    }

    public static Animal WithSpecies(this Animal entity, string species)
    {
        entity.Reclassify(species);
        return entity;
    }

    public static Animal WithAge(this Animal entity, int age)
    {
        entity.ChangeAge(age);
        return entity;
    }
}
