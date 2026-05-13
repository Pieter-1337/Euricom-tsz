namespace Tsz.Api.Modules.Animals;

public class AnimalSeeder(AnimalDbContext context)
{
    public void Seed()
    {
        context.Animals.AddRange(GetAnimals());
        context.SaveChanges();
    }

    private static IReadOnlyList<Animal> GetAnimals() =>
    [
        Animal.Create("Max",        "Dog",      3),
        Animal.Create("Bella",      "Dog",      5),
        Animal.Create("Charlie",    "Dog",      7),
        Animal.Create("Luna",       "Dog",      2),
        Animal.Create("Cooper",     "Dog",      10),
        Animal.Create("Daisy",      "Dog",      4),
        Animal.Create("Buddy",      "Dog",      8),
        Animal.Create("Molly",      "Dog",      1),
        Animal.Create("Rocky",      "Dog",      6),
        Animal.Create("Sadie",      "Dog",      13),
        Animal.Create("Whiskers",   "Cat",      4),
        Animal.Create("Shadow",     "Cat",      7),
        Animal.Create("Mittens",    "Cat",      2),
        Animal.Create("Oliver",     "Cat",      9),
        Animal.Create("Cleo",       "Cat",      14),
        Animal.Create("Simba",      "Cat",      3),
        Animal.Create("Nala",       "Cat",      6),
        Animal.Create("Tiger",      "Cat",      11),
        Animal.Create("Oreo",       "Cat",      1),
        Animal.Create("Leo",        "Lion",     5),
        Animal.Create("Mufasa",     "Lion",     12),
        Animal.Create("Aslan",      "Lion",     9),
        Animal.Create("Sarabi",     "Lion",     10),
        Animal.Create("Raja",       "Tiger",    6),
        Animal.Create("Shere",      "Tiger",    9),
        Animal.Create("Khan",       "Tiger",    11),
        Animal.Create("Dumbo",      "Elephant", 10),
        Animal.Create("Ellie",      "Elephant", 25),
        Animal.Create("Babar",      "Elephant", 55),
        Animal.Create("Geoffrey",   "Giraffe",  12),
        Animal.Create("Stretch",    "Giraffe",  20),
        Animal.Create("Marty",      "Zebra",    9),
        Animal.Create("Baloo",      "Bear",     8),
        Animal.Create("Paddington", "Bear",     5),
        Animal.Create("Akela",      "Wolf",     5),
        Animal.Create("Ghost",      "Wolf",     3),
        Animal.Create("Foxy",       "Fox",      2),
        Animal.Create("Reynard",    "Fox",      10),
    ];
}
