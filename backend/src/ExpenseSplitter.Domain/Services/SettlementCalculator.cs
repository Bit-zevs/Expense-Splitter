using System.Numerics;
using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;

namespace ExpenseSplitter.Domain.Services;

public static class SettlementCalculator
{
    public static IReadOnlyCollection<SettlementTransfer> Calculate(Trip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);

        return CalculateFromBalances(ParticipantBalanceCalculator.Calculate(trip));
    }

    public static IReadOnlyCollection<SettlementTransfer> CalculateFromBalances(
        IReadOnlyCollection<ParticipantBalance> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        var creditors = balances
            .Where(balance => balance.Amount > 0)
            .Select(BalancePosition.FromBalance)
            .ToArray();
        var debtors = balances
            .Where(balance => balance.Amount < 0)
            .Select(BalancePosition.FromBalance)
            .ToArray();
        var transfers = new List<SettlementTransfer>();
        var creditorIndex = 0;
        var debtorIndex = 0;

        while (creditorIndex < creditors.Length && debtorIndex < debtors.Length)
        {
            var creditor = creditors[creditorIndex];
            var debtor = debtors[debtorIndex];
            var transferAmount = BigInteger.Min(-debtor.Amount, creditor.Amount);
            transfers.Add(new SettlementTransfer(
                debtor.ParticipantId,
                creditor.ParticipantId,
                MoneyCents.ToDecimalExact(transferAmount)));

            debtor.Amount += transferAmount;
            creditor.Amount -= transferAmount;

            if (debtor.Amount == 0)
            {
                debtorIndex++;
            }

            if (creditor.Amount == 0)
            {
                creditorIndex++;
            }
        }

        if (creditors.Any(creditor => creditor.Amount != 0)
            || debtors.Any(debtor => debtor.Amount != 0))
        {
            throw new InvalidOperationException("Settlement did not clear all participant balances.");
        }

        return transfers.AsReadOnly();
    }

    private sealed class BalancePosition(Guid participantId, BigInteger amount)
    {
        public Guid ParticipantId { get; } = participantId;

        public BigInteger Amount { get; set; } = amount;

        public static BalancePosition FromBalance(ParticipantBalance balance) =>
            new(balance.ParticipantId, MoneyCents.FromDecimal(balance.Amount));
    }
}
