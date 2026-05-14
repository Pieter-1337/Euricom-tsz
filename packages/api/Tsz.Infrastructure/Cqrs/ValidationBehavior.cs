using FluentValidation;
using Tsz.Infrastructure.Errors;

namespace Tsz.Infrastructure.Cqrs;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
            throw new Tsz.Infrastructure.Errors.ValidationException(failures);

        return await next();
    }
}
