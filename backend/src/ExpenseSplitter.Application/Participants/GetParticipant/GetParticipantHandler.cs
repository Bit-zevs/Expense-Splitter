using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.GetParticipant;

public sealed class GetParticipantHandler(ITripStore tripStore, ICurrentAccount account)
{
    public async Task<ParticipantResult?> HandleAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindWithParticipantsByIdAsync(
            tripId, account.Id, cancellationToken);
        var participant = trip?.Participants.SingleOrDefault(p => p.Id == participantId);

        return participant is null
            ? null
            : new ParticipantResult(participant.Id, participant.Name, participant.AccountId is not null,
                participant.AccountId == trip!.OwnerAccountId);
    }
}
