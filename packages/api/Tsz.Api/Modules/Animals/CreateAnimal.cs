using Tsz.Infrastructure.Abstractions;
using FluentValidation;

namespace Tsz.Api.Modules.Animals;

public sealed record CreateAnimalCommand(string Name, string Species, int Age)
    : ICommand<AnimalDto>;

public sealed class CreateAnimalValidator : AbstractValidator<CreateAnimalCommand>
{
    public CreateAnimalValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Species).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Age).InclusiveBetween(0, 200);
    }
}

public sealed class CreateAnimalHandler(IUnitOfWork uow)
    : ICommandHandler<CreateAnimalCommand, AnimalDto>
{
    public async Task<AnimalDto> HandleAsync(CreateAnimalCommand command, CancellationToken ct = default)
    {
        var animal = Animal.Create(command.Name, command.Species, command.Age);
        uow.RepositoryFor<Animal>().Add(animal);
        await uow.SaveChangesAsync(ct);
        return AnimalDto.ToDto(animal);
    }
}
