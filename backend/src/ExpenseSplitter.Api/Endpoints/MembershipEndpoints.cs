using ExpenseSplitter.Application.Access;

namespace ExpenseSplitter.Api.Endpoints;

public static class MembershipEndpoints
{
    public static void MapMembershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var requests = endpoints.MapGroup("/trip-join-requests").RequireAuthorization();
        requests.MapPost("/", async (JoinRequest request, ITripMembershipService service, CancellationToken ct) =>
        {
            var result = await service.RequestAsync(request.Code, ct);
            return TypedResults.Created($"/trip-join-requests/{result.Id}", result);
        }).RequireRateLimiting("join").ProducesProblem(StatusCodes.Status429TooManyRequests);
        requests.MapGet("/{id:guid}", async (Guid id, ITripMembershipService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetRequestAsync(id, ct)));
        requests.MapDelete("/{id:guid}", async (Guid id, ITripMembershipService service, CancellationToken ct) =>
        {
            await service.CancelAsync(id, ct);
            return TypedResults.NoContent();
        });
        var trips = endpoints.MapGroup("/trips/{tripId:guid}").RequireAuthorization();
        trips.MapPost("/join-code", async (Guid tripId, ITripMembershipService service, CancellationToken ct) =>
            TypedResults.Ok(new { code = await service.RotateCodeAsync(tripId, ct) }));
        trips.MapGet("/join-requests", async (Guid tripId, ITripMembershipService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(tripId, ct)));
        trips.MapPost("/join-requests/{id:guid}/approve", async (Guid tripId, Guid id, ApproveRequest request,
            ITripMembershipService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ApproveAsync(tripId, id, request.ParticipantId, ct)));
        trips.MapDelete("/join-requests/{id:guid}", async (Guid tripId, Guid id, ITripMembershipService service, CancellationToken ct) =>
        {
            await service.RejectAsync(tripId, id, ct);
            return TypedResults.NoContent();
        });
        trips.MapPut("/owner", async (Guid tripId, OwnerRequest request, ITripMembershipService service, CancellationToken ct) =>
        {
            await service.TransferOwnerAsync(tripId, request.AccountId, ct);
            return TypedResults.NoContent();
        });
    }

    public sealed record JoinRequest(string? Code);
    public sealed record ApproveRequest(Guid? ParticipantId = null);
    public sealed record OwnerRequest(Guid AccountId);
}
