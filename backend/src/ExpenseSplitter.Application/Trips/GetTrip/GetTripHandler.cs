using ExpenseSplitter.Application.Access;
namespace ExpenseSplitter.Application.Trips.GetTrip;

public sealed record GetTripResult(
    Guid Id,
    string Name,
    string Currency,
    DateTimeOffset CreatedAt,
    Guid OwnerAccountId);

public sealed class GetTripHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<GetTripResult?> HandleAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ownerOnly: false, write: false, cancellationToken);
        var trip = await tripStore.FindByIdAsync(id, cancellationToken);

        return trip is null
            ? null
            : new GetTripResult(trip.Id, trip.Name, trip.Currency, trip.CreatedAt, trip.OwnerAccountId);
    }
}
