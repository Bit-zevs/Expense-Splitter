using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;

namespace ExpenseSplitter.Domain.Services;

public static class SettlementCalculator
{
    public static IReadOnlyCollection<SettlementTransfer> Calculate(Trip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var balances = CreateBalances(trip);
        ApplyExpenses(trip, balances);

        if (balances.Sum(balance => balance.Amount) != 0m)
        {
            throw new InvalidOperationException("Participant balances must add up to zero.");
        }

        var creditors = balances.Where(balance => balance.Amount > 0m).ToArray();
        var debtors = balances.Where(balance => balance.Amount < 0m).ToArray();
        var transfers = new List<SettlementTransfer>();
        var creditorIndex = 0;
        var debtorIndex = 0;

        while (creditorIndex < creditors.Length && debtorIndex < debtors.Length)
        {
            var creditor = creditors[creditorIndex];
            var debtor = debtors[debtorIndex];
            var transferAmount = Math.Min(-debtor.Amount, creditor.Amount);

            transfers.Add(new SettlementTransfer(
                debtor.ParticipantId,
                creditor.ParticipantId,
                transferAmount));

            debtor.Amount += transferAmount;
            creditor.Amount -= transferAmount;

            if (debtor.Amount == 0m)
            {
                debtorIndex++;
            }

            if (creditor.Amount == 0m)
            {
                creditorIndex++;
            }
        }

        if (creditors.Any(creditor => creditor.Amount != 0m)
            || debtors.Any(debtor => debtor.Amount != 0m))
        {
            throw new InvalidOperationException("Settlement did not clear all participant balances.");
        }

        return transfers.AsReadOnly();
    }

    private static List<ParticipantBalance> CreateBalances(Trip trip)
    {
        var balances = new List<ParticipantBalance>();
        var participantIds = new HashSet<Guid>();

        foreach (var participant in trip.Participants)
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

    private static void ApplyExpenses(Trip trip, IReadOnlyCollection<ParticipantBalance> balances)
    {
        var balancesByParticipantId = balances.ToDictionary(balance => balance.ParticipantId);

        foreach (var expense in trip.Expenses)
        {
            if (!balancesByParticipantId.TryGetValue(expense.PaidByParticipantId, out var payerBalance))
            {
                throw new InvalidOperationException("An expense payer does not belong to the trip.");
            }

            payerBalance.Amount += expense.Amount;

            foreach (var share in expense.Shares)
            {
                if (!balancesByParticipantId.TryGetValue(share.ParticipantId, out var participantBalance))
                {
                    throw new InvalidOperationException(
                        "An expense share participant does not belong to the trip.");
                }

                participantBalance.Amount -= share.Amount;
            }
        }
    }

    private sealed class ParticipantBalance(Guid participantId)
    {
        public Guid ParticipantId { get; } = participantId;

        public decimal Amount { get; set; }
    }
}
