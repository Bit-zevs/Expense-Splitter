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
            var transferAmount = DecimalMoney.Min(-debtor.Amount, creditor.Amount);
            transfers.Add(new SettlementTransfer(
                debtor.ParticipantId,
                creditor.ParticipantId,
                transferAmount.ToDecimalExact()));

            debtor.Amount += transferAmount;
            creditor.Amount -= transferAmount;

            if (debtor.Amount == DecimalMoney.Zero)
            {
                debtorIndex++;
            }

            if (creditor.Amount == DecimalMoney.Zero)
            {
                creditorIndex++;
            }
        }

        if (creditors.Any(creditor => creditor.Amount != DecimalMoney.Zero)
            || debtors.Any(debtor => debtor.Amount != DecimalMoney.Zero))
        {
            throw new InvalidOperationException("Settlement did not clear all participant balances.");
        }

        return transfers.AsReadOnly();
    }

    private sealed class BalancePosition(Guid participantId, DecimalMoney amount)
    {
        public Guid ParticipantId { get; } = participantId;

        public DecimalMoney Amount { get; set; } = amount;

        public static BalancePosition FromBalance(ParticipantBalance balance) =>
            new(balance.ParticipantId, DecimalMoney.FromDecimal(balance.Amount));
    }
}
