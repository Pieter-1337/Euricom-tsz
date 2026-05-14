namespace Tsz.Infrastructure.Errors;

public interface IErrorCode
{
    string Code { get; }
    string Message { get; }
    ErrorCategory Category { get; }
}
