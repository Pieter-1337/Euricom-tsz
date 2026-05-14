using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record UpdateUserLeavesCommand(Guid UserId, int Year, IReadOnlyList<UpdateUserLeavesItem> Items)
    : ICommand<IReadOnlyList<UserLeaveDto>>;

public sealed record UpdateUserLeavesItem(Guid Id, decimal? TotalDays);

public sealed record UpdateUserLeavesBody(int Year, IReadOnlyList<UpdateUserLeavesItem> Items);

public sealed class UpdateUserLeavesValidator : AbstractValidator<UpdateUserLeavesCommand>
{
    private readonly IUnitOfWork _uow;
    private Dictionary<Guid, LeaveAllowed> _allowedByRowId = [];

    public UpdateUserLeavesValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.UserId)
            .NotEmpty().WithError(CommonErrors.Required);

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithError(CommonErrors.Invalid);

        RuleFor(x => x.Items)
            .NotEmpty().WithError(CommonErrors.Required);

        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.Id).Distinct().Count() == items.Count)
            .WithError(CommonErrors.Invalid)
            .When(x => x.Items is { Count: > 0 });

        // Load all referenced rows once; stash in _allowedByRowId for per-item rules.
        RuleFor(x => x)
            .MustAsync(LoadAndCheckAllRowsExistAsync)
            .WithError(UserLeaveErrors.NotFound)
            .When(x => x.UserId != Guid.Empty && x.Year >= 2000 && x.Year <= 2100 && x.Items is { Count: > 0 })
            .OverridePropertyName("items");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Id)
                .NotEmpty().WithError(CommonErrors.Required);

            item.RuleFor(i => i.TotalDays)
                .GreaterThanOrEqualTo(0m).WithError(CommonErrors.Invalid)
                .When(i => i.TotalDays.HasValue);

            item.RuleFor(i => i)
                .Must(i =>
                {
                    if (!_allowedByRowId.TryGetValue(i.Id, out var allowed)) return true;
                    if (allowed == LeaveAllowed.Limited && i.TotalDays is null) return false;
                    if (allowed != LeaveAllowed.Limited && i.TotalDays is not null) return false;
                    return true;
                })
                .WithError(UserLeaveErrors.TotalDaysAllowedMismatch)
                .OverridePropertyName("totalDays")
                .When(i => i.Id != Guid.Empty);
        });
    }

    private async Task<bool> LoadAndCheckAllRowsExistAsync(UpdateUserLeavesCommand command, CancellationToken ct)
    {
        var itemIds = command.Items.Select(i => i.Id).ToHashSet();
        var rows = (await _uow.RepositoryFor<UserLeave>()
            .GetAllAsListAsync(ul => ul.UserId == command.UserId && ul.Year == command.Year && itemIds.Contains(ul.Id), ct: ct)).ToList();

        if (rows.Count != itemIds.Count)
        {
            _allowedByRowId = [];
            return false;
        }

        var leaveTypeIds = rows.Select(r => r.LeaveTypeId).ToHashSet();
        var leaveTypes = await _uow.RepositoryFor<LeaveType>()
            .GetAllAsListAsync(lt => leaveTypeIds.Contains(lt.Id), ct: ct);
        var ltById = leaveTypes.ToDictionary(lt => lt.Id);

        _allowedByRowId = rows.ToDictionary(r => r.Id, r => ltById[r.LeaveTypeId].DefaultAllowed);
        return true;
    }
}

public sealed class UpdateUserLeavesHandler(IUnitOfWork uow)
    : ICommandHandler<UpdateUserLeavesCommand, IReadOnlyList<UserLeaveDto>>
{
    public async Task<IReadOnlyList<UserLeaveDto>> HandleAsync(UpdateUserLeavesCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<UserLeave>();
        var ids = command.Items.Select(i => i.Id).ToHashSet();

        var rows = await repo.GetAllAsListAsync(
            ul => ul.UserId == command.UserId && ul.Year == command.Year && ids.Contains(ul.Id), ct);

        var byId = rows.ToDictionary(r => r.Id);
        foreach (var item in command.Items)
            byId[item.Id].SetTotalDays(item.TotalDays);

        await uow.SaveChangesAsync(ct);

        var allRows = await repo.GetAllAsListAsync(
            ul => ul.UserId == command.UserId && ul.Year == command.Year, ct);
        var leaveTypes = (await uow.RepositoryFor<LeaveType>().GetAllAsListAsync(ct: ct))
            .ToDictionary(lt => lt.Id);

        return allRows
            .Select(r => UserLeaveDto.ToDto(r, leaveTypes[r.LeaveTypeId].Name, leaveTypes[r.LeaveTypeId].DefaultAllowed))
            .ToList();
    }
}
