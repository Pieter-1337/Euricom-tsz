using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Users.Auth;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Auth;

/// Validates the <see cref="ImpersonationHeader.Name"/> header and, when all trust checks pass,
/// sets the request-scoped <see cref="IImpersonationContext"/> so the effective
/// <see cref="ICurrentUserResolver"/> loads the target identity.
///
/// Must be registered between <c>UseAuthentication()</c> and <c>UseAuthorization()</c>.
/// Absent header → no-op (identical to today).
public sealed class ImpersonationMiddleware(ILogger<ImpersonationMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Headers.TryGetValue(ImpersonationHeader.Name, out var headerValues)
            || string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
        {
            await next(context);
            return;
        }

        var rawTargetId = headerValues.First()!.Trim();

        var realResolver = context.RequestServices.GetRequiredService<IRealUserResolver>();
        var realUser = await realResolver.ResolveRealAsync(context.RequestAborted);

        if (realUser is null || !realUser.HasRole(nameof(UserRole.Admin)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!Guid.TryParse(rawTargetId, out var targetId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var uow = context.RequestServices.GetRequiredService<IUnitOfWork>();
        var target = await uow.RepositoryFor<User>().FirstOrDefaultAsync(u => u.Id == targetId, context.RequestAborted);

        if (target is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (target.Roles.Contains(UserRole.Admin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var impersonationContext = context.RequestServices.GetRequiredService<IImpersonationContext>();
        impersonationContext.SetTarget(targetId);

        logger.LogInformation(
            "Impersonation engaged: realUserId={RealUserId} target={TargetId}",
            realUser.Id,
            targetId);

        await next(context);
    }
}
