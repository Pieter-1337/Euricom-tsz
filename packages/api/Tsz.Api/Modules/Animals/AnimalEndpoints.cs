using Tsz.Api.Common.Extensions;
using Tsz.Api.Common.Filters;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Animals;

public static class AnimalEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("animals");

        group.MapGet("/", async (
            IQueryHandler<GetAnimalsQuery, IReadOnlyList<AnimalDto>> handler,
            CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new GetAnimalsQuery(), ct)));

        group.MapGet("/{id:guid}", async (
            Guid id,
            IQueryHandler<GetAnimalByIdQuery, AnimalDto?> handler,
            CancellationToken ct) =>
        {
            var animal = await handler.HandleAsync(new GetAnimalByIdQuery(id), ct);
            return animal is not null ? Results.Ok(animal) : Results.NotFound();
        });

        group.MapPost("/", async (
            CreateAnimalCommand command,
            ICommandHandler<CreateAnimalCommand, AnimalDto> handler,
            CancellationToken ct) =>
        {
            var animal = await handler.HandleAsync(command, ct);
            return Results.Created($"/api/animals/{animal.Id}", animal);
        }).AddEndpointFilter<ValidationFilter<CreateAnimalCommand>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateAnimalCommand command,
            ICommandHandler<UpdateAnimalCommand, AnimalDto?> handler,
            CancellationToken ct) =>
        {
            if (command.Id != id)
                return Results.BadRequest("Route id does not match command id.");

            var animal = await handler.HandleAsync(command, ct);
            return animal is not null ? Results.Ok(animal) : Results.NotFound();
        }).AddEndpointFilter<ValidationFilter<UpdateAnimalCommand>>();

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteAnimalCommand, bool> handler,
            CancellationToken ct) =>
                await handler.HandleAsync(new DeleteAnimalCommand(id), ct)
                    ? Results.NoContent()
                    : Results.NotFound());
    }
}
