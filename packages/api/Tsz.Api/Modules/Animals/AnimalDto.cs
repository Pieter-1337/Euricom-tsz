using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Animals;

public sealed record AnimalDto(Guid Id, string Name, string Species, int Age)
    : IEntityDto<Animal, AnimalDto>
{
    public static Expression<Func<Animal, AnimalDto>> Project =>
        a => new AnimalDto(a.Id, a.Name, a.Species, a.Age);

    public static AnimalDto ToDto(Animal entity) =>
        new(entity.Id, entity.Name, entity.Species, entity.Age);
}
