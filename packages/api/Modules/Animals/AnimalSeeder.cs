namespace Api.Modules.Animals;

public class AnimalSeeder(AnimalDbContext context)
{
    public void Seed()
    {
        context.Animals.AddRange(GetAnimals());
        context.SaveChanges();
    }

    private static IReadOnlyList<Animal> GetAnimals() => new List<Animal>
    {
            // Dogs (1–15)
            new() { Name = "Max",     Species = "Dog", Age = 3  },
            new() { Name = "Bella",   Species = "Dog", Age = 5  },
            new() { Name = "Charlie", Species = "Dog", Age = 7  },
            new() { Name = "Luna",    Species = "Dog", Age = 2  },
            new() { Name = "Cooper",  Species = "Dog", Age = 10 },
            new() { Name = "Daisy",   Species = "Dog", Age = 4  },
            new() { Name = "Buddy",   Species = "Dog", Age = 8  },
            new() { Name = "Molly",   Species = "Dog", Age = 1  },
            new() { Name = "Rocky",   Species = "Dog", Age = 6  },
            new() { Name = "Sadie",   Species = "Dog", Age = 13 },

            // Cats (1–20)
            new() { Name = "Whiskers", Species = "Cat", Age = 4  },
            new() { Name = "Shadow",   Species = "Cat", Age = 7  },
            new() { Name = "Mittens",  Species = "Cat", Age = 2  },
            new() { Name = "Oliver",   Species = "Cat", Age = 9  },
            new() { Name = "Cleo",     Species = "Cat", Age = 14 },
            new() { Name = "Simba",    Species = "Cat", Age = 3  },
            new() { Name = "Nala",     Species = "Cat", Age = 6  },
            new() { Name = "Tiger",    Species = "Cat", Age = 11 },
            new() { Name = "Oreo",     Species = "Cat", Age = 1  },
            new() { Name = "Luna",     Species = "Cat", Age = 18 },

            // Lions (2–16)
            new() { Name = "Leo",      Species = "Lion", Age = 5  },
            new() { Name = "Kion",     Species = "Lion", Age = 3  },
            new() { Name = "Mufasa",   Species = "Lion", Age = 12 },
            new() { Name = "Nala",     Species = "Lion", Age = 7  },
            new() { Name = "Aslan",    Species = "Lion", Age = 9  },
            new() { Name = "Simba",    Species = "Lion", Age = 2  },
            new() { Name = "Sarabi",   Species = "Lion", Age = 10 },
            new() { Name = "Zira",     Species = "Lion", Age = 8  },
            new() { Name = "Vitani",   Species = "Lion", Age = 4  },
            new() { Name = "Chumvi",   Species = "Lion", Age = 16 },

            // Tigers (2–18)
            new() { Name = "Raja",     Species = "Tiger", Age = 6  },
            new() { Name = "Shere",    Species = "Tiger", Age = 9  },
            new() { Name = "Kira",     Species = "Tiger", Age = 3  },
            new() { Name = "Bandar",   Species = "Tiger", Age = 14 },
            new() { Name = "Zara",     Species = "Tiger", Age = 7  },
            new() { Name = "Khan",     Species = "Tiger", Age = 11 },
            new() { Name = "Reza",     Species = "Tiger", Age = 2  },
            new() { Name = "Tara",     Species = "Tiger", Age = 8  },
            new() { Name = "Indra",    Species = "Tiger", Age = 16 },
            new() { Name = "Veda",     Species = "Tiger", Age = 5  },

            // Elephants (5–60)
            new() { Name = "Dumbo",    Species = "Elephant", Age = 10 },
            new() { Name = "Ellie",    Species = "Elephant", Age = 25 },
            new() { Name = "Jumbo",    Species = "Elephant", Age = 40 },
            new() { Name = "Nelly",    Species = "Elephant", Age = 18 },
            new() { Name = "Babar",    Species = "Elephant", Age = 55 },
            new() { Name = "Tembo",    Species = "Elephant", Age = 7  },
            new() { Name = "Asha",     Species = "Elephant", Age = 30 },
            new() { Name = "Mara",     Species = "Elephant", Age = 12 },
            new() { Name = "Kesi",     Species = "Elephant", Age = 48 },
            new() { Name = "Zola",     Species = "Elephant", Age = 22 },

            // Giraffes (3–25)
            new() { Name = "Spots",    Species = "Giraffe", Age = 5  },
            new() { Name = "Geoffrey", Species = "Giraffe", Age = 12 },
            new() { Name = "Patches",  Species = "Giraffe", Age = 8  },
            new() { Name = "Stretch",  Species = "Giraffe", Age = 20 },
            new() { Name = "Twiga",    Species = "Giraffe", Age = 3  },
            new() { Name = "Amarula",  Species = "Giraffe", Age = 15 },
            new() { Name = "Jirafu",   Species = "Giraffe", Age = 7  },
            new() { Name = "Kesi",     Species = "Giraffe", Age = 24 },
            new() { Name = "Rafiki",   Species = "Giraffe", Age = 10 },
            new() { Name = "Zuri",     Species = "Giraffe", Age = 18 },

            // Zebras (3–25)
            new() { Name = "Stripe",   Species = "Zebra", Age = 4  },
            new() { Name = "Marty",    Species = "Zebra", Age = 9  },
            new() { Name = "Zeb",      Species = "Zebra", Age = 15 },
            new() { Name = "Dazzle",   Species = "Zebra", Age = 6  },
            new() { Name = "Punda",    Species = "Zebra", Age = 22 },
            new() { Name = "Burchell", Species = "Zebra", Age = 3  },
            new() { Name = "Quagga",   Species = "Zebra", Age = 11 },
            new() { Name = "Haki",     Species = "Zebra", Age = 18 },
            new() { Name = "Farasi",   Species = "Zebra", Age = 7  },
            new() { Name = "Ndovu",    Species = "Zebra", Age = 25 },

            // Bears (3–30)
            new() { Name = "Baloo",    Species = "Bear", Age = 8  },
            new() { Name = "Paddington", Species = "Bear", Age = 5 },
            new() { Name = "Winnie",   Species = "Bear", Age = 12 },
            new() { Name = "Grizzly",  Species = "Bear", Age = 20 },
            new() { Name = "Kodiak",   Species = "Bear", Age = 15 },
            new() { Name = "Nanook",   Species = "Bear", Age = 3  },
            new() { Name = "Ursa",     Species = "Bear", Age = 27 },
            new() { Name = "Bruno",    Species = "Bear", Age = 9  },
            new() { Name = "Mishka",   Species = "Bear", Age = 18 },
            new() { Name = "Koda",     Species = "Bear", Age = 6  },

            // Wolves (2–14)
            new() { Name = "Akela",    Species = "Wolf", Age = 5  },
            new() { Name = "Ghost",    Species = "Wolf", Age = 3  },
            new() { Name = "Nymeria",  Species = "Wolf", Age = 7  },
            new() { Name = "Grey",     Species = "Wolf", Age = 10 },
            new() { Name = "Fenrir",   Species = "Wolf", Age = 12 },
            new() { Name = "Lupa",     Species = "Wolf", Age = 2  },
            new() { Name = "Balto",    Species = "Wolf", Age = 8  },
            new() { Name = "Timber",   Species = "Wolf", Age = 6  },
            new() { Name = "Blaze",    Species = "Wolf", Age = 4  },
            new() { Name = "Shadow",   Species = "Wolf", Age = 14 },

            // Foxes (1–12)
            new() { Name = "Foxy",     Species = "Fox", Age = 2  },
            new() { Name = "Tod",      Species = "Fox", Age = 5  },
            new() { Name = "Vixen",    Species = "Fox", Age = 8  },
            new() { Name = "Reynard",  Species = "Fox", Age = 10 },
            new() { Name = "Copper",   Species = "Fox", Age = 3  },
            new() { Name = "Zorro",    Species = "Fox", Age = 7  },
            new() { Name = "Amber",    Species = "Fox", Age = 1  },
            new() { Name = "Rusty",    Species = "Fox", Age = 12 },
            new() { Name = "Blaze",    Species = "Fox", Age = 4  },
            new() { Name = "Tails",    Species = "Fox", Age = 6  },
        };
}
