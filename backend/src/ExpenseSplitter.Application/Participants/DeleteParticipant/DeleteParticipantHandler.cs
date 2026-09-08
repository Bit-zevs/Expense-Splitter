using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Participants.DeleteParticipant;

public sealed class DeleteParticipantHandler(ITripStore tripStore)
{
    public async Task<bool> HandleAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindWithParticipantsAndExpensesForUpdateAsync(
            tripId,
            cancellationToken);

        if (trip is null || !trip.RemoveParticipant(participantId))
        {
            return false;
        }

        await tripStore.SaveChangesAsync(cancellationToken);
        return true;
    }
}
