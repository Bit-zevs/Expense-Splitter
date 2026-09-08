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

        foreach (var balance in balances)
        {
            balance.Amount = MoneyCents.ToDecimalExact(balancesByParticipantId[balance.ParticipantId]);
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
        balances[expense.PaidByParticipantId] += MoneyCents.FromDecimal(expense.Amount);
        foreach (var share in expense.Shares)
        {
            if (!balances.ContainsKey(share.ParticipantId))
                throw new InvalidOperationException("An expense share participant does not belong to the trip.");
            balances[share.ParticipantId] -= MoneyCents.FromDecimal(share.Amount);
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
