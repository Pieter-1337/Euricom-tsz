using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Modules.LeaveTypes;

public sealed class LeaveTypeErrors : ErrorCodeBase<LeaveTypeErrors>
{
    private LeaveTypeErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly LeaveTypeErrors NotFound =
        new("ERR_LEAVE_TYPE_NOT_FOUND", "Leave type not found.", ErrorCategory.NotFound);

    public static readonly LeaveTypeErrors NameAlreadyExists =
        new("ERR_LEAVE_TYPE_NAME_ALREADY_EXISTS", "A leave type with this name already exists.", ErrorCategory.Conflict);

    public static readonly LeaveTypeErrors InUse =
        new("ERR_LEAVE_TYPE_IN_USE", "This leave type is in use and cannot be deleted.", ErrorCategory.Conflict);
}
