using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.GetParticipant;

public sealed class GetParticipantHandler(ITripStore tripStore)
{
    public async Task<ParticipantResult?> HandleAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        var participant = await tripStore.FindParticipantByIdAsync(
            tripId,
            participantId,
            cancellationToken);

        return participant is null
            ? null
            : new ParticipantResult(participant.Id, participant.Name);
    }
}
