using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.GetParticipants;

public sealed class GetParticipantsHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<IReadOnlyCollection<ParticipantResult>?> HandleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        await access.RequireAsync(tripId, ownerOnly: false, write: false, cancellationToken);
        var trip = await tripStore.FindWithParticipantsByIdAsync(tripId, cancellationToken);

        return trip?.Participants
            .Select(participant => new ParticipantResult(participant.Id, participant.Name, participant.AccountId))
            .ToArray();
    }
}
