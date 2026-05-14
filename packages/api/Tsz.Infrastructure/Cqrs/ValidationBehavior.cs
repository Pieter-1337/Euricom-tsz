using FluentValidation;
using Tsz.Infrastructure.Errors;

namespace Tsz.Infrastructure.Cqrs;

public sealed class ValidationBehavior<TCommand, TResponse>(IEnumerable<IValidator<TCommand>> validators)
    : IPipelineBehavior<TCommand, TResponse>
{
    public async Task<TResponse> HandleAsync(TCommand command, Func<Task<TResponse>> next, CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TCommand>(command);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
            throw new Tsz.Infrastructure.Errors.ValidationException(failures);

        return await next();
    }
}
