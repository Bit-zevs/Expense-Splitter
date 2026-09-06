using ExpenseSplitter.Application.Participants;
using ExpenseSplitter.Application.Participants.AddParticipant;
using ExpenseSplitter.Application.Participants.GetParticipants;

namespace ExpenseSplitter.Api.Endpoints;

public static class ParticipantEndpoints
{
    public static IEndpointRouteBuilder MapParticipantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/trips/{tripId:guid}/participants", AddParticipantAsync)
            .WithName("AddParticipant")
            .WithSummary("Adds a participant to a trip")
            .Produces<ParticipantResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("/trips/{tripId:guid}/participants", GetParticipantsAsync)
            .WithName("GetParticipants")
            .WithSummary("Gets a trip's participants")
            .Produces<IReadOnlyCollection<ParticipantResult>>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> AddParticipantAsync(
        Guid tripId,
        AddParticipantRequest request,
        AddParticipantHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(tripId, request.Name, cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Created($"/trips/{tripId}/participants/{result.Id}", result);
        }
        catch (ArgumentException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Participant name is required."]
            });
        }
    }

    private static async Task<IResult> GetParticipantsAsync(
        Guid tripId,
        GetParticipantsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tripId, cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    public sealed record AddParticipantRequest(string? Name);
}
