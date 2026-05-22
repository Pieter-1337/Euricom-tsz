using Microsoft.AspNetCore.Authorization;

namespace Tsz.Infrastructure.Auth;

public sealed class RequireAdminOrAnyClientManagerRequirement : IAuthorizationRequirement;
