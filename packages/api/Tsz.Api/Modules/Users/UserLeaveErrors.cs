using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Modules.Users;

public sealed class UserLeaveErrors : ErrorCodeBase<UserLeaveErrors>
{
    private UserLeaveErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly UserLeaveErrors NotFound =
        new("ERR_USER_LEAVE_NOT_FOUND", "User leave not found.", ErrorCategory.NotFound);

    public static readonly UserLeaveErrors TotalDaysAllowedMismatch =
        new("ERR_USER_LEAVE_TOTAL_ALLOWED_MISMATCH",
            "TotalDays must be set for Limited leave types and null otherwise.",
            ErrorCategory.Validation);
}
