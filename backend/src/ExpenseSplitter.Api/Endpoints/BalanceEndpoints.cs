using ExpenseSplitter.Application.Balances.GetBalances;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseSplitter.Api.Endpoints;

public static class BalanceEndpoints
{
    public static IEndpointRouteBuilder MapBalanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/trips/{tripId:guid}/balances", GetBalancesAsync)
            .WithName("GetBalances")
            .WithSummary("Gets all or selected participant balances for a trip")
            .Produces<IReadOnlyCollection<ParticipantBalanceResult>>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetBalancesAsync(
        Guid tripId,
        [FromQuery] Guid[]? participantIds,
        GetBalancesHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(
                tripId,
                participantIds,
                cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "participantIds"] = [exception.Message]
            });
        }
    }
}
