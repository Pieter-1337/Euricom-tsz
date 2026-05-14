using FluentValidation;
using FluentValidation.Results;

namespace Tsz.Infrastructure.Errors;

public class ValidationException : FluentValidation.ValidationException
{
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base(failures) { }
}
