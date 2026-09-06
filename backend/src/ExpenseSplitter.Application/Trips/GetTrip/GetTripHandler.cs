namespace ExpenseSplitter.Application.Trips.GetTrip;

public sealed record GetTripResult(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed class GetTripHandler(ITripStore tripStore)
{
    public async Task<GetTripResult?> HandleAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindByIdAsync(id, cancellationToken);

        return trip is null
            ? null
            : new GetTripResult(trip.Id, trip.Name, trip.CreatedAt);
    }
}
