using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record DeleteUserCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteUserValidator : AbstractValidator<DeleteUserCommand>
{
    private readonly IUnitOfWork _uow;

    public DeleteUserValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Id)
            .MustAsync(UserExists).WithError(UserErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
    }

    private async Task<bool> UserExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<User>().ExistsAsync(u => u.Id == id, ct);
}

public sealed class DeleteUserHandler(IUnitOfWork uow, TimeProvider time)
    : ICommandHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> HandleAsync(DeleteUserCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<User>();
        var user = await repo.GetByIdAsync(command.Id, ct);
        user!.SoftDelete(time.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return default;
    }
}
