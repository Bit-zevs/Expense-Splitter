using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.AddParticipant;

public sealed class AddParticipantHandler(ITripStore tripStore)
{
    public async Task<ParticipantResult?> HandleAsync(
        Guid tripId,
        string? name,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindForUpdateAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var participant = trip.AddParticipant(name!);
        await tripStore.SaveChangesAsync(cancellationToken);

        return new ParticipantResult(participant.Id, participant.Name);
    }
}
