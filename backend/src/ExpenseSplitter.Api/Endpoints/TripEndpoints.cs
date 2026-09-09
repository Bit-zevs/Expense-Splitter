using ExpenseSplitter.Application.Trips.GetTripSnapshot;
using ExpenseSplitter.Application.Trips.CreateTrip;
using ExpenseSplitter.Application.Trips.DeleteTrip;
using ExpenseSplitter.Application.Trips.GetTrip;

namespace ExpenseSplitter.Api.Endpoints;

public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/trips", CreateTripAsync)
            .WithName("CreateTrip")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Creates a trip")
            .Produces<CreateTripResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        endpoints.MapGet("/trips/{id:guid}", GetTripAsync)
            .WithName("GetTrip")
            .WithSummary("Gets a trip by ID")
            .Produces<GetTripResult>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapDelete("/trips/{id:guid}", DeleteTripAsync)
            .WithName("DeleteTrip")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Deletes a trip and all its data")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("/trips/{id:guid}/snapshot", async (
            Guid id, GetTripSnapshotHandler handler, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
            .WithName("GetTripSnapshot")
            .WithSummary("Gets trip data and calculation from one database snapshot")
            .Produces<TripSnapshotResult>()
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
                new CreateTripCommand(request.Name, request.Currency),
                cancellationToken);

            return Results.Created($"/trips/{result.Id}", result);
        }
        catch (ArgumentException exception)
        {
            var field = exception.ParamName == "currency" ? "currency" : "name";
            var message = field == "currency"
                ? "Currency must be one of: RUB, EUR, USD."
                : "Trip name is required.";

            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [field] = [message]
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

    private static async Task<IResult> DeleteTripAsync(
        Guid id,
        DeleteTripHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
    }

    public sealed record CreateTripRequest(string? Name, string? Currency = null);
}
