using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Modules.Users;

public sealed class UserErrors : ErrorCodeBase<UserErrors>
{
    private UserErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly UserErrors EmailAlreadyExists =
        new("ERR_USER_EMAIL_ALREADY_EXISTS", "A user with this email already exists.", ErrorCategory.Conflict);

    public static readonly UserErrors NotFound =
        new("ERR_USER_NOT_FOUND", "User not found.", ErrorCategory.NotFound);
}
