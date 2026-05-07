using Api.Common.Extensions;
using Api.Common.Filters;

namespace Api.Modules.Animals;

public static class AnimalEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapApiGroup("animals");

        group.MapGet("/", async (AnimalService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{id:int}", async (int id, AnimalService service, CancellationToken ct) =>
        {
            var animal = await service.GetByIdAsync(id, ct);
            return animal is not null
                ? Results.Ok(animal)
                : Results.NotFound();
        });

        group.MapPost("/", async (CreateAnimalRequest request, AnimalService service, CancellationToken ct) =>
        {
            var animal = await service.CreateAsync(request, ct);
            return Results.Created($"/api/animals/{animal.Id}", animal);
        }).AddEndpointFilter<ValidationFilter<CreateAnimalRequest>>();

        group.MapPut("/{id:int}", async (int id, UpdateAnimalRequest request, AnimalService service, CancellationToken ct) =>
        {
            var animal = await service.UpdateAsync(id, request, ct);
            return animal is not null
                ? Results.Ok(animal)
                : Results.NotFound();
        }).AddEndpointFilter<ValidationFilter<UpdateAnimalRequest>>();

        group.MapDelete("/{id:int}", async (int id, AnimalService service, CancellationToken ct) =>
        {
            return await service.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.NotFound();
        });
    }
}
