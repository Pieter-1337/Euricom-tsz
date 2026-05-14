using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text.Json.Serialization;
using Tsz.Api.Modules.Users;
using Tsz.Api.Persistence;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAuthentication()
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.MapInboundClaims = false;

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
}
else
{
    builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.MapInboundClaims = false;
    });
}

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new RequireAdminRequirement());
    });
});

builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer((schema, _, _) =>
    {
        // Remove spurious string type from numeric schemas (JsonNumberHandling artefact)
        if (schema.Type.HasValue &&
            (schema.Type.Value & (JsonSchemaType.Integer | JsonSchemaType.Number)) != 0 &&
            (schema.Type.Value & JsonSchemaType.String) != 0)
        {
            schema.Type &= ~JsonSchemaType.String;
        }

        // Mark all non-nullable properties as required so TS omits the `?`
        if (schema.Properties is { Count: > 0 })
        {
            schema.Required ??= new HashSet<string>();
            foreach (var (name, property) in schema.Properties)
            {
                var isNullable = property.Type is { } t && (t & JsonSchemaType.Null) != 0;
                if (!isNullable)
                    schema.Required.Add(name);
            }
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
           ?? "Data Source=tsz.db");
});
builder.Services.AddInfrastructure<AppDbContext>();
builder.Services.AddHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();
builder.Services.AddScoped<IAuthorizationHandler, RequireAdminAuthorizationHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
    if (app.Environment.IsDevelopment())
    {
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await new UserSeeder(uow).SeedAsync();
    }
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.MapScalarApiReference("/openapi", options =>
{
    options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
}).AllowAnonymous();

app.MapGet("/", () => new
{
    name = "Tsz API",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
}).AllowAnonymous();

UserEndpoints.Map(app);


app.Run();
