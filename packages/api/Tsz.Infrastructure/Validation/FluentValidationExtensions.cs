using FluentValidation;
using Tsz.Infrastructure.Errors;

namespace Tsz.Infrastructure.Validation;

public static class FluentValidationExtensions
{
    public static IRuleBuilderOptions<T, TProp> WithError<T, TProp, TEnum>(
        this IRuleBuilderOptions<T, TProp> rule,
        ErrorCodeBase<TEnum> code)
        where TEnum : ErrorCodeBase<TEnum>
    {
        return rule
            .WithErrorCode(code.Code)
            .WithMessage(code.Message)
            .WithState(_ => code);
    }
}
