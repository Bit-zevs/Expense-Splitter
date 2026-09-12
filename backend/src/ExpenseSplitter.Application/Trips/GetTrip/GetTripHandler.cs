using ExpenseSplitter.Application.Access;
namespace ExpenseSplitter.Application.Trips.GetTrip;

public sealed record GetTripResult(
    Guid Id,
    string Name,
    string Currency,
    DateTimeOffset CreatedAt,
    bool IsOwner);

public sealed class GetTripHandler(ITripStore tripStore, ICurrentAccount account)
{
    public async Task<GetTripResult?> HandleAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindByIdAsync(id, account.Id, cancellationToken);

        return trip is null
            ? null
            : new GetTripResult(trip.Id, trip.Name, trip.Currency, trip.CreatedAt, trip.OwnerAccountId == account.Id);
    }
}
