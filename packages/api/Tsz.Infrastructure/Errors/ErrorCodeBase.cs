using System.Collections.Concurrent;
using System.Reflection;

namespace Tsz.Infrastructure.Errors;

public abstract class ErrorCodeBase<TEnum> : IErrorCode
    where TEnum : ErrorCodeBase<TEnum>
{
    private static readonly ConcurrentDictionary<string, TEnum> Registry = new(StringComparer.Ordinal);

    public string Code { get; }
    public string Message { get; }
    public ErrorCategory Category { get; }

    protected ErrorCodeBase(string code, string message, ErrorCategory category)
    {
        if (!code.StartsWith("ERR_", StringComparison.Ordinal))
            throw new ArgumentException($"Error code must start with 'ERR_'. Got: '{code}'", nameof(code));

        Code = code;
        Message = message;
        Category = category;

        Registry[code] = (TEnum)this;
    }

    public static TEnum? FromCode(string code)
    {
        EnsureStaticFieldsInitialized();
        return Registry.GetValueOrDefault(code);
    }

    private static void EnsureStaticFieldsInitialized()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TEnum).TypeHandle);
    }

    public override string ToString() => Code;
}
