using ExpenseSplitter.Application.Access;
namespace ExpenseSplitter.Application.Trips.DeleteTrip;

public sealed class DeleteTripHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<bool> HandleAsync(Guid tripId, CancellationToken cancellationToken)
    {
        await access.RequireWriteAsync(tripId, ownerOnly: true, cancellationToken);
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
