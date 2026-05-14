namespace Tsz.Infrastructure.Errors;

public sealed class CommonErrors : ErrorCodeBase<CommonErrors>
{
    private CommonErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly CommonErrors Required =
        new("ERR_REQUIRED", "'{PropertyName}' is required.", ErrorCategory.Validation);

    public static readonly CommonErrors Invalid =
        new("ERR_INVALID", "'{PropertyName}' has an invalid value.", ErrorCategory.Validation);

    public static readonly CommonErrors InvalidEmail =
        new("ERR_INVALID_EMAIL", "'{PropertyName}' must be a valid email address.", ErrorCategory.Validation);
}
