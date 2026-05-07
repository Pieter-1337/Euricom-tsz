using Microsoft.EntityFrameworkCore;

namespace Api.Modules.Animals;

public class AnimalDbContext : DbContext
{
    public AnimalDbContext(DbContextOptions<AnimalDbContext> options) : base(options)
    {
    }

    public DbSet<Animal> Animals => Set<Animal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AnimalConfiguration());
    }
}
