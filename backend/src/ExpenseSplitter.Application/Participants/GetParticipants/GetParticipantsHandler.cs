using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.GetParticipants;

public sealed class GetParticipantsHandler(ITripStore tripStore, ICurrentAccount account)
{
    public async Task<IReadOnlyCollection<ParticipantResult>?> HandleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindWithParticipantsByIdAsync(tripId, account.Id, cancellationToken);

        return trip?.Participants
            .Select(participant => new ParticipantResult(
                participant.Id, participant.Name, participant.AccountId is not null,
                participant.AccountId == trip.OwnerAccountId))
            .ToArray();
    }
}
