using FluentValidation;
using Tsz.Api.Modules.Users;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.LeaveTypes.Features;

public sealed record DeleteLeaveTypeCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteLeaveTypeValidator : AbstractValidator<DeleteLeaveTypeCommand>
{
    private readonly IUnitOfWork _uow;

    public DeleteLeaveTypeValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Id)
            .MustAsync(LeaveTypeExists).WithError(LeaveTypeErrors.NotFound)
            .When(x => x.Id != Guid.Empty);
        RuleFor(x => x.Id)
            .MustAsync(NotInUse).WithError(LeaveTypeErrors.InUse)
            .When(x => x.Id != Guid.Empty);
    }

    private async Task<bool> LeaveTypeExists(Guid id, CancellationToken ct) =>
        await _uow.RepositoryFor<LeaveType>().ExistsAsync(lt => lt.Id == id, ct);

    private async Task<bool> NotInUse(Guid id, CancellationToken ct) =>
        !await _uow.RepositoryFor<UserLeave>().ExistsAsync(ul => ul.LeaveTypeId == id, ct);
}

public sealed class DeleteLeaveTypeHandler(IUnitOfWork uow)
    : ICommandHandler<DeleteLeaveTypeCommand, Unit>
{
    public async Task<Unit> HandleAsync(DeleteLeaveTypeCommand command, CancellationToken ct = default)
    {
        var lt = await uow.RepositoryFor<LeaveType>().GetByIdAsync(command.Id, ct);
        uow.RepositoryFor<LeaveType>().Remove(lt!);
        await uow.SaveChangesAsync(ct);
        return default;
    }
}
