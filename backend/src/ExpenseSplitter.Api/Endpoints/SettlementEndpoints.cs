using ExpenseSplitter.Application.Settlements.GetSettlements;

namespace ExpenseSplitter.Api.Endpoints;

public static class SettlementEndpoints
{
    public static IEndpointRouteBuilder MapSettlementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/trips/{tripId:guid}/settlements", GetSettlementsAsync)
            .WithName("GetSettlements")
            .WithSummary("Gets participant balances and a settlement plan for a trip")
            .Produces<GetSettlementsResult>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetSettlementsAsync(
        Guid tripId,
        GetSettlementsHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(tripId, cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        }
        catch (OverflowException)
        {
            return Results.Problem(
                title: "Balance exceeds the supported response range.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
