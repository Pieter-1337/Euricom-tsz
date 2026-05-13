using Tsz.Infrastructure.Abstractions;
using FluentValidation;

namespace Tsz.Api.Modules.Animals;

public sealed record UpdateAnimalCommand(Guid Id, string Name, string Species, int Age)
    : ICommand<AnimalDto?>;

public sealed class UpdateAnimalValidator : AbstractValidator<UpdateAnimalCommand>
{
    public UpdateAnimalValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Species).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Age).InclusiveBetween(0, 200);
    }
}

public sealed class UpdateAnimalHandler(IUnitOfWork uow)
    : ICommandHandler<UpdateAnimalCommand, AnimalDto?>
{
    public async Task<AnimalDto?> HandleAsync(UpdateAnimalCommand command, CancellationToken ct = default)
    {
        var animal = await uow.RepositoryFor<Animal>().GetByIdAsync(command.Id, ct);
        if (animal is null) return null;

        animal.Rename(command.Name);
        animal.Reclassify(command.Species);
        animal.ChangeAge(command.Age);

        await uow.SaveChangesAsync(ct);
        return AnimalDto.ToDto(animal);
    }
}
