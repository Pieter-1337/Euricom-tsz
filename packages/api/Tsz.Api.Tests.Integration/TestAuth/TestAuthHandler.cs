using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tsz.Api.Tests.Integration.TestAuth;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    /// Default oid used when <c>X-Test-Oid</c> header is absent.
    public const string DefaultOid = "test-user-id";

    /// Default email used when <c>X-Test-Email</c> header is absent.
    public const string DefaultEmail = "test@test.com";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Per-request overrides let integration tests simulate different real callers
        // (e.g. admin A vs non-admin B) while also sending X-Impersonate-User.
        var oid = Request.Headers.TryGetValue("X-Test-Oid", out var oidValues)
                  && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault())
            ? oidValues.First()!
            : DefaultOid;

        var email = Request.Headers.TryGetValue("X-Test-Email", out var emailValues)
                    && !string.IsNullOrWhiteSpace(emailValues.FirstOrDefault())
            ? emailValues.First()!
            : DefaultEmail;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, oid),
            new Claim("sub", oid),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim("name", "Test User"),
            new Claim(ClaimTypes.Email, email),
            new Claim("email", email),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
