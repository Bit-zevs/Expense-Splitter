using System.Numerics;
using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;

namespace ExpenseSplitter.Domain.Services;

public static class ParticipantBalanceCalculator
{
    public static IReadOnlyCollection<ParticipantBalance> Calculate(Trip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var balances = CreateBalances(trip);
        var balancesByParticipantId = balances.ToDictionary(balance => balance.ParticipantId, _ => BigInteger.Zero);

        foreach (var expense in trip.Expenses)
        {
            ApplyExpense(expense, balancesByParticipantId);
        }

        var maximumCents = new BigInteger(MoneyLimits.MaximumAmount * 100m);
        foreach (var balance in balances)
        {
            var cents = balancesByParticipantId[balance.ParticipantId];
            if (BigInteger.Abs(cents) > maximumCents)
                throw new OverflowException("The final balance exceeds the exact monetary range.");
            balance.Amount = (decimal)cents / 100m;
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
        IDictionary<Guid, BigInteger> balances)
    {
        if (!balances.ContainsKey(expense.PaidByParticipantId))
            throw new InvalidOperationException("An expense payer does not belong to the trip.");
        balances[expense.PaidByParticipantId] += new BigInteger(expense.Amount * 100m);
        foreach (var share in expense.Shares)
        {
            if (!balances.ContainsKey(share.ParticipantId))
                throw new InvalidOperationException("An expense share participant does not belong to the trip.");
            balances[share.ParticipantId] -= new BigInteger(share.Amount * 100m);
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
