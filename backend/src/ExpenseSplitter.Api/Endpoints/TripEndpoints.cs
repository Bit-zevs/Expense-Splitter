using ExpenseSplitter.Application.Trips.CreateTrip;
using ExpenseSplitter.Application.Trips.GetTrip;

namespace ExpenseSplitter.Api.Endpoints;

public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/trips", CreateTripAsync)
            .WithName("CreateTrip")
            .WithSummary("Creates a trip")
            .Produces<CreateTripResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        endpoints.MapGet("/trips/{id:guid}", GetTripAsync)
            .WithName("GetTrip")
            .WithSummary("Gets a trip by ID")
            .Produces<GetTripResult>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateTripAsync(
        CreateTripRequest request,
        CreateTripHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(
                new CreateTripCommand(request.Name),
                cancellationToken);

            return Results.Created($"/trips/{result.Id}", result);
        }
        catch (ArgumentException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Trip name is required."]
            });
        }
    }

    private static async Task<IResult> GetTripAsync(
        Guid id,
        GetTripHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    public sealed record CreateTripRequest(string? Name);
}
