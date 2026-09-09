using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.GetParticipant;

public sealed class GetParticipantHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<ParticipantResult?> HandleAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        await access.RequireAsync(tripId, ownerOnly: false, write: false, cancellationToken);
        var participant = await tripStore.FindParticipantByIdAsync(
            tripId,
            participantId,
            cancellationToken);

        return participant is null
            ? null
            : new ParticipantResult(participant.Id, participant.Name, participant.AccountId);
    }
}
