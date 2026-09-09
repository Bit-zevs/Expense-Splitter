using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Services;

namespace ExpenseSplitter.Application.Balances.GetBalances;

public sealed record ParticipantBalanceResult(
    Guid ParticipantId,
    string Name,
    decimal Amount);

public sealed class GetBalancesHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<IReadOnlyCollection<ParticipantBalanceResult>?> HandleAsync(
        Guid tripId,
        IReadOnlyCollection<Guid>? participantIds,
        CancellationToken cancellationToken)
    {
        await access.RequireAsync(tripId, ownerOnly: false, write: false, cancellationToken);
        var trip = await tripStore.FindWithParticipantsAndExpensesByIdAsync(
            tripId,
            cancellationToken);
        if (trip is null)
        {
            return null;
        }

        HashSet<Guid>? selectedIds = null;
        if (participantIds is { Count: > 0 })
        {
            if (participantIds.Any(id => id == Guid.Empty))
            {
                throw new ArgumentException(
                    "Participant IDs cannot be empty.",
                    nameof(participantIds));
            }

            selectedIds = participantIds.ToHashSet();
            if (selectedIds.Count != participantIds.Count)
            {
                throw new ArgumentException(
                    "Selected participants must be unique.",
                    nameof(participantIds));
            }

            var tripParticipantIds = trip.Participants
                .Select(participant => participant.Id)
                .ToHashSet();
            if (selectedIds.Any(id => !tripParticipantIds.Contains(id)))
            {
                throw new ArgumentException(
                    "All selected participants must belong to the trip.",
                    nameof(participantIds));
            }
        }

        var namesById = trip.Participants.ToDictionary(
            participant => participant.Id,
            participant => participant.Name);

        return ParticipantBalanceCalculator.Calculate(trip)
            .Where(balance => selectedIds is null || selectedIds.Contains(balance.ParticipantId))
            .Select(balance => new ParticipantBalanceResult(
                balance.ParticipantId,
                namesById[balance.ParticipantId],
                balance.Amount))
            .ToArray();
    }
}
