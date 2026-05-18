using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Tsz.Api.Infrastructure;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Infrastructure;

public class GlobalExceptionHandlerTests
{
    private static (GlobalExceptionHandler handler, Mock<ILogger<GlobalExceptionHandler>> loggerMock)
        Build()
    {
        var loggerMock = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(loggerMock.Object);
        return (handler, loggerMock);
    }

    private static HttpContext BuildHttpContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext ctx)
    {
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(ctx.Response.Body);
    }

    private sealed class ConflictErrors : ErrorCodeBase<ConflictErrors>
    {
        private ConflictErrors(string code, string message, ErrorCategory category)
            : base(code, message, category) { }

        public static readonly ConflictErrors DuplicateEmail =
            new("ERR_TEST_EMAIL_CONFLICT", "Email already exists.", ErrorCategory.Conflict);
    }

    private sealed class NotFoundErrors : ErrorCodeBase<NotFoundErrors>
    {
        private NotFoundErrors(string code, string message, ErrorCategory category)
            : base(code, message, category) { }

        public static readonly NotFoundErrors Missing =
            new("ERR_TEST_NOT_FOUND", "Not found.", ErrorCategory.NotFound);
    }

    private static FluentValidation.ValidationException MakeValidationException(
        string property,
        IErrorCode? errorCode,
        string errorMessage = "fail")
    {
        var failure = new ValidationFailure(property, errorMessage)
        {
            CustomState = errorCode,
            ErrorCode = errorCode?.Code ?? "ERR_GENERIC",
        };
        return new FluentValidation.ValidationException([failure]);
    }

    [Fact]
    public async Task ValidationException_SingleConflict_Returns409()
    {
        var (handler, _) = Build();
        var ctx = BuildHttpContext();
        var ex = MakeValidationException("Email", ConflictErrors.DuplicateEmail);

        var handled = await handler.TryHandleAsync(ctx, ex, default);

        handled.ShouldBeTrue();
        ctx.Response.StatusCode.ShouldBe(409);
        ctx.Response.ContentType.ShouldStartWith("application/problem+json");

        var doc = await ReadBodyAsync(ctx);
        doc.RootElement.GetProperty("status").GetInt32().ShouldBe(409);
        doc.RootElement.GetProperty("code").GetString().ShouldBe("ERR_TEST_EMAIL_CONFLICT");
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty("Email", out var emailErrors).ShouldBeTrue();
        emailErrors[0].GetProperty("code").GetString().ShouldBe("ERR_TEST_EMAIL_CONFLICT");
    }

    [Fact]
    public async Task ValidationException_MixedNotFoundAndValidation_Returns404()
    {
        var (handler, _) = Build();
        var ctx = BuildHttpContext();

        var notFoundFailure = new ValidationFailure("Id", "not found")
        {
            CustomState = NotFoundErrors.Missing,
            ErrorCode = NotFoundErrors.Missing.Code,
        };
        var validationFailure = new ValidationFailure("Email", "required")
        {
            CustomState = CommonErrors.Required,
            ErrorCode = CommonErrors.Required.Code,
        };
        var ex = new FluentValidation.ValidationException([notFoundFailure, validationFailure]);

        await handler.TryHandleAsync(ctx, ex, default);

        ctx.Response.StatusCode.ShouldBe(404);
        var doc = await ReadBodyAsync(ctx);
        doc.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty("Id", out _).ShouldBeTrue();
        errors.TryGetProperty("Email", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UnhandledException_Returns500_WithNoDetailLeaked()
    {
        var (handler, loggerMock) = Build();
        var ctx = BuildHttpContext();
        var ex = new InvalidOperationException("super-secret-internal-detail");

        var handled = await handler.TryHandleAsync(ctx, ex, default);

        handled.ShouldBeTrue();
        ctx.Response.StatusCode.ShouldBe(500);

        var doc = await ReadBodyAsync(ctx);
        doc.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
        doc.RootElement.GetProperty("title").GetString().ShouldBe("An unexpected error occurred.");
        doc.RootElement.TryGetProperty("detail", out _).ShouldBeFalse();

        var body = JsonSerializer.Serialize(doc.RootElement);
        body.ShouldNotContain("super-secret-internal-detail");
    }

    [Fact]
    public async Task UnhandledException_LogsAtErrorLevel()
    {
        var (handler, loggerMock) = Build();
        var ctx = BuildHttpContext();
        var ex = new Exception("boom");

        await handler.TryHandleAsync(ctx, ex, default);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                ex,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidationException_LogsAtInformationLevel()
    {
        var (handler, loggerMock) = Build();
        var ctx = BuildHttpContext();
        var ex = MakeValidationException("Prop", CommonErrors.Required);

        await handler.TryHandleAsync(ctx, ex, default);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<FluentValidation.ValidationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_AlwaysReturnsTrue()
    {
        var (handler, _) = Build();
        var ctx1 = BuildHttpContext();
        var ctx2 = BuildHttpContext();

        var r1 = await handler.TryHandleAsync(ctx1, new FluentValidation.ValidationException([]), default);
        var r2 = await handler.TryHandleAsync(ctx2, new Exception("boom"), default);

        r1.ShouldBeTrue();
        r2.ShouldBeTrue();
    }
}
