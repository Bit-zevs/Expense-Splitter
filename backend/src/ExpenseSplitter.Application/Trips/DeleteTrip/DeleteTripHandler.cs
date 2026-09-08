namespace ExpenseSplitter.Application.Trips.DeleteTrip;

public sealed class DeleteTripHandler(ITripStore tripStore)
{
    public async Task<bool> HandleAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindTrackedAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return false;
        }

        tripStore.Remove(trip);
        await tripStore.SaveChangesAsync(cancellationToken);
        return true;
    }
}
