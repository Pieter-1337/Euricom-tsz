using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record DeleteUserLeaveCommand(Guid UserId, Guid Id) : ICommand<Unit>;

public sealed class DeleteUserLeaveValidator : AbstractValidator<DeleteUserLeaveCommand>
{
    private readonly IUnitOfWork _uow;

    public DeleteUserLeaveValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Id)
            .MustAsync(RowExists).WithError(UserLeaveErrors.NotFound)
            .When(x => x.Id != Guid.Empty && x.UserId != Guid.Empty);
    }

    private async Task<bool> RowExists(DeleteUserLeaveCommand command, Guid id, ValidationContext<DeleteUserLeaveCommand> ctx, CancellationToken ct) =>
        await _uow.RepositoryFor<UserLeave>().ExistsAsync(ul => ul.Id == id && ul.UserId == command.UserId, ct);
}

public sealed class DeleteUserLeaveHandler(IUnitOfWork uow)
    : ICommandHandler<DeleteUserLeaveCommand, Unit>
{
    public async Task<Unit> HandleAsync(DeleteUserLeaveCommand command, CancellationToken ct = default)
    {
        var entity = await uow.RepositoryFor<UserLeave>()
            .FirstOrDefaultAsync(ul => ul.Id == command.Id && ul.UserId == command.UserId, ct);
        uow.RepositoryFor<UserLeave>().Remove(entity!);
        await uow.SaveChangesAsync(ct);
        return default;
    }
}
