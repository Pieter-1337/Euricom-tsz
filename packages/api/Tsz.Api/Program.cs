using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Tsz.Api.Extensions;
using Tsz.Api.Infrastructure;
using Tsz.Api.Persistence;
using Tsz.Infrastructure;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Extensions;
using Tsz.Modules.Contracts;
using Tsz.Modules.Customers;
using Tsz.Modules.LeaveTypes;
using Tsz.Modules.Users;
using Tsz.Modules.Users.Seeding;

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

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

IReadOnlyList<IModule> modules = [new UsersModule(), new CustomersModule(), new ContractsModule(), new LeaveTypesModule()];
foreach (var m in modules) m.RegisterServices(builder.Services, builder.Configuration);

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
        var seeder = scope.ServiceProvider.GetRequiredService<UserSeeder>();
        await seeder.SeedAsync();
    }
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapTszOpenApi();

foreach (var m in modules) m.MapEndpoints(app);

app.Run();
