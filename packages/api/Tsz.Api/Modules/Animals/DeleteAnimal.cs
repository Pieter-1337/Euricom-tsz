using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Animals;

public sealed record DeleteAnimalCommand(Guid Id) : ICommand<bool>;

public sealed class DeleteAnimalHandler(IUnitOfWork uow)
    : ICommandHandler<DeleteAnimalCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteAnimalCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<Animal>();
        var animal = await repo.GetByIdAsync(command.Id, ct);
        if (animal is null) return false;

        repo.Remove(animal);
        await uow.SaveChangesAsync(ct);
        return true;
    }
}
