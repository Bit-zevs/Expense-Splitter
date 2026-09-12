using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.AddParticipant;

public sealed class AddParticipantHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<ParticipantResult?> HandleAsync(
        Guid tripId,
        string? name,
        CancellationToken cancellationToken)
    {
        await access.RequireWriteAsync(tripId, ownerOnly: true, cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        var trip = await tripStore.FindTrackedAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var participant = trip.AddParticipant(name!);
        await tripStore.SaveChangesAsync(cancellationToken);

        return new ParticipantResult(participant.Id, participant.Name, IsRegistered: false, IsOwner: false);
    }
}
