using ExpenseSplitter.Application.Trips.CreateTrip;

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

    public sealed record CreateTripRequest(string? Name);
}
