using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;
using Tsz.SharedKernel;

namespace Tsz.Modules.Contracts.Features;

public sealed record UpdateContractCommand(
    Guid Id,
    string Subject,
    Guid? ClientManagerId,
    DateOnly Start,
    DateOnly? End,
    IReadOnlyList<Guid> ConsultantIds,
    IReadOnlyList<UpdateContractTaskDto>? Tasks = null)
    : ICommand<ContractDto>;

public sealed class UpdateContractValidator : AbstractValidator<UpdateContractCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IUsersAccessModule _users;

    public UpdateContractValidator(IUnitOfWork uow, IUsersAccessModule users)
    {
        _uow = uow;
        _users = users;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(256);

        RuleFor(x => x)
            .Must(x => x.End is null || x.End.Value >= x.Start)
            .WithError(ContractErrors.EndBeforeStart);

        When(x => x.ClientManagerId is not null, () =>
        {
            RuleFor(x => x.ClientManagerId!.Value)
                .MustAsync(UserExists)
                    .WithError(ContractErrors.ClientManagerNotFound)
                .MustAsync(UserHasClientManagerRole)
                    .WithError(ContractErrors.ClientManagerMissingRole);
        });

        RuleFor(x => x.ConsultantIds)
            .NotNull()
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithError(ContractErrors.ConsultantDuplicates)
            .When(x => x.ConsultantIds is { Count: > 0 });

        RuleFor(x => x)
            .MustAsync(NewConsultantsExist)
            .WithError(ContractErrors.ConsultantNotFound)
            .When(x => x.Id != Guid.Empty && x.ConsultantIds is { Count: > 0 }
                       && x.ConsultantIds.Distinct().Count() == x.ConsultantIds.Count);

        RuleFor(x => x.Id)
            .MustAsync(ContractExists).WithError(ContractErrors.NotFound)
            .When(x => x.Id != Guid.Empty);

        When(x => x.Tasks is not null, () =>
        {
            RuleForEach(x => x.Tasks!).ChildRules(t =>
            {
                t.RuleFor(i => i.Name)
                    .NotEmpty().WithError(ContractErrors.TaskNameRequired)
                    .MaximumLength(256).WithError(ContractErrors.TaskNameTooLong);
                t.RuleFor(i => i.Rate)
                    .GreaterThan(0).WithError(ContractErrors.TaskRateNonPositive);
            });

            RuleFor(x => x.Tasks!)
                .Must(HaveUniqueActiveNames)
                    .WithError(ContractErrors.DuplicateTaskName)
                .When(x => x.Tasks!.All(i =>
                    !string.IsNullOrWhiteSpace(i.Name) && i.Name.Length <= 256));

            RuleFor(x => x)
                .MustAsync(AllPayloadIdsExistOnContract)
                    .WithError(ContractErrors.TaskNotFound)
                .When(x => x.Id != Guid.Empty
                           && x.Tasks!.Any(i => i.Id.HasValue));
        });
    }

    private Task<bool> ContractExists(Guid id, CancellationToken ct) =>
        _uow.RepositoryFor<Contract>().ExistsAsync(c => c.Id == id, ct);

    private Task<bool> UserExists(Guid id, CancellationToken ct) =>
        _users.ExecuteQueryAsync(new UserExistsQuery(id), ct);

    private Task<bool> UserHasClientManagerRole(Guid id, CancellationToken ct) =>
        _users.ExecuteQueryAsync(new UserHasRoleQuery(id, UserRole.ClientManager), ct);

    private async Task<bool> NewConsultantsExist(UpdateContractCommand cmd, CancellationToken ct)
    {
        var existing = await _uow.RepositoryFor<Contract>()
            .FirstOrDefaultAsync(c => c.Id == cmd.Id, ct);
        if (existing is null) return true;

        var already = existing.ConsultantIds.ToHashSet();
        var newIds = cmd.ConsultantIds.Where(id => !already.Contains(id));
        foreach (var id in newIds)
        {
            if (!await _users.ExecuteQueryAsync(new UserExistsQuery(id), ct))
                return false;
        }
        return true;
    }

    private static bool HaveUniqueActiveNames(IReadOnlyList<UpdateContractTaskDto> tasks)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tasks)
            if (!seen.Add(t.Name))
                return false;
        return true;
    }

    private async Task<bool> AllPayloadIdsExistOnContract(
        UpdateContractCommand cmd,
        CancellationToken ct)
    {
        var contract = await _uow.RepositoryFor<Contract>()
            .FirstOrDefaultAsync(c => c.Id == cmd.Id, ct);
        if (contract is null) return true;

        var known = contract.Tasks.Select(t => t.Id).ToHashSet();
        return cmd.Tasks!
            .Where(i => i.Id.HasValue)
            .All(i => known.Contains(i.Id!.Value));
    }
}

public sealed class UpdateContractHandler(IUnitOfWork uow)
    : ICommandHandler<UpdateContractCommand, ContractDto>
{
    public async Task<ContractDto> HandleAsync(UpdateContractCommand command, CancellationToken ct = default)
    {
        var contract = await uow.RepositoryFor<Contract>().GetByIdAsync(command.Id, ct);

        contract!.Rename(command.Subject.Trim());
        contract.AssignClientManager(command.ClientManagerId);
        contract.UpdatePeriod(command.Start, command.End);
        contract.ReplaceConsultants(command.ConsultantIds);

        if (command.Tasks is not null)
            contract.ApplyTasks(command.Tasks, DateTimeOffset.UtcNow);

        await uow.SaveChangesAsync(ct);
        return ContractDto.ToDto(contract);
    }
}
