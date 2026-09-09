using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.DeleteParticipant;

public sealed class DeleteParticipantHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<bool> HandleAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        await access.RequireAsync(tripId, ownerOnly: true, write: true, cancellationToken);
        var trip = await tripStore.FindWithParticipantsAndExpensesTrackedAsync(
            tripId,
            cancellationToken);

        if (trip?.Participants.Any(p => p.Id == participantId && p.AccountId == trip.OwnerAccountId) == true)
            throw new ConflictException("Transfer ownership before removing the owner.");

        if (trip is null || !trip.RemoveParticipant(participantId))
        {
            return false;
        }

        await tripStore.SaveChangesAsync(cancellationToken);
        return true;
    }
}
