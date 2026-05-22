using Tsz.Infrastructure.Errors;

namespace Tsz.Modules.Contracts.Domain.Contracts;

public sealed class ContractErrors : ErrorCodeBase<ContractErrors>
{
    private ContractErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly ContractErrors NotFound =
        new("ERR_CONTRACT_NOT_FOUND", "Contract not found.", ErrorCategory.NotFound);

    public static readonly ContractErrors CustomerNotFound =
        new("ERR_CONTRACT_CUSTOMER_NOT_FOUND",
            "The assigned customer does not exist.",
            ErrorCategory.NotFound);

    public static readonly ContractErrors ClientManagerNotFound =
        new("ERR_CONTRACT_CLIENT_MANAGER_NOT_FOUND",
            "The assigned client manager does not exist.",
            ErrorCategory.NotFound);

    public static readonly ContractErrors ClientManagerMissingRole =
        new("ERR_CONTRACT_CLIENT_MANAGER_MISSING_ROLE",
            "The assigned user does not have the ClientManager role.",
            ErrorCategory.Validation);

    public static readonly ContractErrors EndBeforeStart =
        new("ERR_CONTRACT_END_BEFORE_START",
            "Contract end date must be on or after the start date.",
            ErrorCategory.Validation);

    public static readonly ContractErrors ConsultantDuplicates =
        new("ERR_CONTRACT_CONSULTANT_DUPLICATES",
            "Consultant ids must not contain duplicates.",
            ErrorCategory.Validation);

    public static readonly ContractErrors ConsultantNotFound =
        new("ERR_CONTRACT_CONSULTANT_NOT_FOUND",
            "A consultant being added does not exist.",
            ErrorCategory.NotFound);

    public static readonly ContractErrors TaskNotFound =
        new("ERR_CONTRACT_TASK_NOT_FOUND",
            "A task id in the payload does not exist on this contract.",
            ErrorCategory.Validation);

    public static readonly ContractErrors DuplicateTaskName =
        new("ERR_CONTRACT_DUPLICATE_TASK_NAME",
            "Active task names must be unique on a contract (case-insensitive).",
            ErrorCategory.Validation);

    public static readonly ContractErrors TaskNameRequired =
        new("ERR_CONTRACT_TASK_NAME_REQUIRED",
            "Task name is required.",
            ErrorCategory.Validation);

    public static readonly ContractErrors TaskNameTooLong =
        new("ERR_CONTRACT_TASK_NAME_TOO_LONG",
            "Task name must be 256 characters or fewer.",
            ErrorCategory.Validation);

    public static readonly ContractErrors TaskRateNonPositive =
        new("ERR_CONTRACT_TASK_RATE_NON_POSITIVE",
            "Task rate must be greater than zero.",
            ErrorCategory.Validation);

    public static readonly ContractErrors ClientManagerReassignmentForbidden =
        new("ERR_CONTRACT_CLIENT_MANAGER_REASSIGNMENT_FORBIDDEN",
            "Non-admin users may only assign contracts to themselves.",
            ErrorCategory.Forbidden);
}
