using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Modules.Users;

public sealed class UserLeaveErrors : ErrorCodeBase<UserLeaveErrors>
{
    private UserLeaveErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly UserLeaveErrors NotFound =
        new("ERR_USER_LEAVE_NOT_FOUND", "User leave not found.", ErrorCategory.NotFound);

    public static readonly UserLeaveErrors UserNotFound =
        new("ERR_USER_LEAVE_USER_NOT_FOUND", "User not found.", ErrorCategory.NotFound);

    public static readonly UserLeaveErrors LeaveTypeNotFound =
        new("ERR_USER_LEAVE_LEAVE_TYPE_NOT_FOUND", "Leave type not found.", ErrorCategory.NotFound);

    public static readonly UserLeaveErrors Duplicate =
        new("ERR_USER_LEAVE_DUPLICATE", "A leave record for this user, leave type, and year already exists.", ErrorCategory.Conflict);
}
