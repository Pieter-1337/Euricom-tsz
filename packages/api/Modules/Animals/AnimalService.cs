
using Microsoft.EntityFrameworkCore;

namespace Api.Modules.Animals;

public class AnimalService
{
    private readonly AnimalDbContext _db;

    public AnimalService(AnimalDbContext db)
    {
        _db = db;
    }

    public Task<List<Animal>> GetAllAsync(CancellationToken ct = default) =>
        _db.Animals.ToListAsync(ct);

    public Task<Animal?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Animals.FindAsync([id], ct).AsTask();

    public async Task<Animal> CreateAsync(CreateAnimalRequest request, CancellationToken ct = default)
    {
        var animal = new Animal
        {
            Name = request.Name,
            Species = request.Species,
            Age = request.Age
        };
        await _db.Animals.AddAsync(animal, ct);
        await _db.SaveChangesAsync(ct);
        return animal;
    }

    public async Task<Animal?> UpdateAsync(int id, UpdateAnimalRequest request, CancellationToken ct = default)
    {
        var animal = await _db.Animals.FindAsync([id], ct);
        if (animal is null) return null;

        animal.Name = request.Name;
        animal.Species = request.Species;
        animal.Age = request.Age;
        await _db.SaveChangesAsync(ct);
        return animal;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var animal = await _db.Animals.FindAsync([id], ct);
        if (animal is null) return false;

        _db.Animals.Remove(animal);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
