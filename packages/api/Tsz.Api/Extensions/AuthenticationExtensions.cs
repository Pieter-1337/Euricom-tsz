using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Tsz.Infrastructure.Auth;

namespace Tsz.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddTszAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddAuthentication()
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
        });

        if (environment.IsDevelopment())
            services.AddJwtDebugHooks();

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new RequireAdminRequirement());
            });

            options.AddPolicy(AuthorizationPolicies.RequireClientManager, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new RequireClientManagerRequirement());
            });
        });

        return services;
    }

    private static IServiceCollection AddJwtDebugHooks(this IServiceCollection services)
    {
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var previousOnFailed = options.Events.OnAuthenticationFailed;
            options.Events.OnAuthenticationFailed = async ctx =>
            {
                await previousOnFailed(ctx);
                var logger = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtDebug");

                var auth = ctx.Request.Headers.Authorization.ToString();
                var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? auth["Bearer ".Length..]
                    : auth;

                logger.LogWarning("JWT auth failed: {Exception}", ctx.Exception?.ToString());
                logger.LogWarning("Raw token: {Token}", token);
            };

            var previousOnValidated = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async ctx =>
            {
                await previousOnValidated(ctx);
                var logger = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtDebug");
                logger.LogInformation("JWT validated. iss={Iss} aud={Aud} tid={Tid}",
                    ctx.Principal?.FindFirst("iss")?.Value,
                    ctx.Principal?.FindFirst("aud")?.Value,
                    ctx.Principal?.FindFirst("tid")?.Value);
            };
        });

        return services;
    }
}
