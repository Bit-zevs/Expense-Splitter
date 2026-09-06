using ExpenseSplitter.Domain.Entities;
using System.Numerics;

namespace ExpenseSplitter.Domain.Services;

internal static class ParticipantBalanceCalculator
{
    public static IReadOnlyCollection<ParticipantBalance> Calculate(Trip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var balances = CreateBalances(trip);
        var balancesByParticipantId = balances.ToDictionary(balance => balance.ParticipantId);

        foreach (var expense in trip.Expenses)
        {
            ApplyExpense(expense, balancesByParticipantId);
        }

        return balances.AsReadOnly();
    }

    private static List<ParticipantBalance> CreateBalances(Trip trip)
    {
        var balances = new List<ParticipantBalance>();
        var participantIds = new HashSet<Guid>();

        foreach (var participant in trip.Participants.OrderBy(participant => participant.Id))
        {
            if (participant.Id == Guid.Empty || !participantIds.Add(participant.Id))
            {
                throw new InvalidOperationException(
                    "Trip participants must have unique, non-empty IDs.");
            }

            balances.Add(new ParticipantBalance(participant.Id));
        }

        return balances;
    }

    private static void ApplyExpense(
        Expense expense,
        IReadOnlyDictionary<Guid, ParticipantBalance> balances)
    {
        var changes = new Dictionary<Guid, BigInteger>();

        if (!balances.ContainsKey(expense.PaidByParticipantId))
        {
            throw new InvalidOperationException("An expense payer does not belong to the trip.");
        }

        changes[expense.PaidByParticipantId] = ToCents(expense.Amount);

        foreach (var share in expense.Shares)
        {
            if (!balances.ContainsKey(share.ParticipantId))
            {
                throw new InvalidOperationException(
                    "An expense share participant does not belong to the trip.");
            }

            changes.TryGetValue(share.ParticipantId, out var currentChange);
            changes[share.ParticipantId] = currentChange - ToCents(share.Amount);
        }

        foreach (var (participantId, change) in changes)
        {
            balances[participantId].AmountInCents += change;
        }
    }

    private static BigInteger ToCents(decimal amount) => new(amount * 100m);
}

internal sealed class ParticipantBalance(Guid participantId)
{
    public Guid ParticipantId { get; } = participantId;

    public BigInteger AmountInCents { get; set; }
}
