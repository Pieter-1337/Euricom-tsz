using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Tsz.Api.Extensions;
using Tsz.Api.Infrastructure;
using Tsz.Api.Modules.Users;
using Tsz.Api.Persistence;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Extensions;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddTszAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddTszOpenApi();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
           ?? "Data Source=tsz.db");
});
builder.Services.AddInfrastructure<AppDbContext>();
builder.Services.AddDispatcher();
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
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
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        await new UserSeeder(uow, timeProvider).SeedAsync();
    }
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapTszEndpoints();

app.Run();
