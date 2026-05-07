using System.Reflection;
using Api.Modules.Animals;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AnimalDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
           ?? "Data Source=animals.db");
});
builder.Services.AddScoped<AnimalService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AnimalDbContext>();
    db.Database.EnsureCreated();
    if (!db.Animals.Any())
        new AnimalSeeder(db).Seed();
}


// app.UseHttpsRedirection();
app.MapOpenApi("/openapi/{documentName}.json");
app.MapScalarApiReference("/openapi", options =>
{
    options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
});

app.MapGet("/", () => new
{
    name = "Animal API",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
});

AnimalEndpoints.Map(app);

app.Run();
