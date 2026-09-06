using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.GetParticipants;

public sealed class GetParticipantsHandler(ITripStore tripStore)
{
    public async Task<IReadOnlyCollection<ParticipantResult>?> HandleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindAggregateByIdAsync(tripId, cancellationToken);

        return trip?.Participants
            .Select(participant => new ParticipantResult(participant.Id, participant.Name))
            .ToArray();
    }
}
