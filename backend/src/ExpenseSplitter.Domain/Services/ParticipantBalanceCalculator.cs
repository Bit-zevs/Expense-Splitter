using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;

namespace ExpenseSplitter.Domain.Services;

public static class ParticipantBalanceCalculator
{
    public static IReadOnlyCollection<ParticipantBalance> Calculate(Trip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var balances = CreateBalances(trip);
        var balancesByParticipantId = balances.ToDictionary(balance => balance.ParticipantId, _ => new Contributions());

        foreach (var expense in trip.Expenses)
        {
            ApplyExpense(expense, balancesByParticipantId);
        }

        foreach (var balance in balances)
        {
            balance.Amount = balancesByParticipantId[balance.ParticipantId].Calculate();
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
        IDictionary<Guid, Contributions> balances)
    {
        if (!balances.ContainsKey(expense.PaidByParticipantId))
            throw new InvalidOperationException("An expense payer does not belong to the trip.");
        balances[expense.PaidByParticipantId].Credits.Add(expense.Amount);
        foreach (var share in expense.Shares)
        {
            if (!balances.ContainsKey(share.ParticipantId))
                throw new InvalidOperationException("An expense share participant does not belong to the trip.");
            balances[share.ParticipantId].Debits.Add(share.Amount);
        }
    }
    private sealed class Contributions
    {
        public List<decimal> Credits { get; } = [];
        public List<decimal> Debits { get; } = [];

        public decimal Calculate()
        {
            // Cancel opposing single-expense amounts before summation, so a valid
            // final balance does not depend on a transient overflow or expense order.
            var credit = 0;
            var debit = 0;
            while (credit < Credits.Count && debit < Debits.Count)
            {
                var cancelled = Math.Min(Credits[credit], Debits[debit]);
                Credits[credit] -= cancelled;
                Debits[debit] -= cancelled;
                if (Credits[credit] == 0) credit++;
                if (Debits[debit] == 0) debit++;
            }
            var total = DecimalMoney.Zero;
            for (; credit < Credits.Count; credit++) total += DecimalMoney.FromDecimal(Credits[credit]);
            for (; debit < Debits.Count; debit++) total -= DecimalMoney.FromDecimal(Debits[debit]);
            return total.ToDecimalExact();
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
