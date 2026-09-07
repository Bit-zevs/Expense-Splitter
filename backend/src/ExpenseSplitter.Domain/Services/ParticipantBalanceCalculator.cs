using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;

namespace ExpenseSplitter.Domain.Services;

public static class ParticipantBalanceCalculator
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
        var changes = new Dictionary<Guid, decimal>();

        if (!balances.ContainsKey(expense.PaidByParticipantId))
        {
            throw new InvalidOperationException("An expense payer does not belong to the trip.");
        }

        changes[expense.PaidByParticipantId] = expense.Amount;

        foreach (var share in expense.Shares)
        {
            if (!balances.ContainsKey(share.ParticipantId))
            {
                throw new InvalidOperationException(
                    "An expense share participant does not belong to the trip.");
            }

            changes.TryGetValue(share.ParticipantId, out var currentChange);
            changes[share.ParticipantId] = MoneyLimits.AddExact(currentChange, -share.Amount);
        }

        foreach (var (participantId, change) in changes)
        {
            var balance = balances[participantId];
            balance.Amount = MoneyLimits.AddExact(balance.Amount, change);
        }
    }
}

public sealed class ParticipantBalance
{
    internal ParticipantBalance(Guid participantId)
    {
        ParticipantId = participantId;
    }

    public Guid ParticipantId { get; }

    public decimal Amount { get; internal set; }
}
