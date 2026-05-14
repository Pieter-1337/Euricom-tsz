using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is FluentValidation.ValidationException ve)
        {
            logger.LogInformation(ve, "Validation failed for request {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

            await WriteValidationProblemAsync(httpContext, ve, cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled exception for request {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        await WriteUnexpectedProblemAsync(httpContext, cancellationToken);
        return true;
    }

    private static async Task WriteValidationProblemAsync(
        HttpContext httpContext,
        FluentValidation.ValidationException ve,
        CancellationToken ct)
    {
        var failures = ve.Errors.ToList();

        var status = failures.Count == 0
            ? 400
            : PickStatus(failures);

        var top = failures
            .OrderByDescending(f => Severity((f.CustomState as IErrorCode)?.Category ?? ErrorCategory.Validation))
            .FirstOrDefault();

        var topCode = (top?.CustomState as IErrorCode)?.Code;
        var topDetail = top?.ErrorMessage;

        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => (object)g.Select(f =>
                {
                    var errorCode = (f.CustomState as IErrorCode)?.Code ?? f.ErrorCode ?? "ERR_UNKNOWN";
                    var message = f.ErrorMessage;
                    return new { code = errorCode, message };
                }).ToArray());

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = ReasonPhrase(status),
            Status = status,
            Detail = topDetail,
        };

        problem.Extensions["code"] = topCode;
        problem.Extensions["errors"] = errors;

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions),
            ct);
    }

    private static async Task WriteUnexpectedProblemAsync(HttpContext httpContext, CancellationToken ct)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = "An unexpected error occurred.",
            Status = 500,
        };

        httpContext.Response.StatusCode = 500;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions),
            ct);
    }

    private static int PickStatus(IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        var highestSeverity = failures
            .Select(f => f.CustomState as IErrorCode)
            .Where(c => c is not null)
            .Select(c => c!.Category)
            .DefaultIfEmpty(ErrorCategory.Validation)
            .OrderByDescending(Severity)
            .First();

        return highestSeverity switch
        {
            ErrorCategory.NotFound => 404,
            ErrorCategory.Conflict => 409,
            ErrorCategory.Forbidden => 403,
            _ => 400,
        };
    }

    private static int Severity(ErrorCategory category) => category switch
    {
        ErrorCategory.NotFound => 4,
        ErrorCategory.Conflict => 3,
        ErrorCategory.Forbidden => 2,
        ErrorCategory.Validation => 1,
        _ => 0,
    };

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        500 => "Internal Server Error",
        _ => "Error",
    };
}
