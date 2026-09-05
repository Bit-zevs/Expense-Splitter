using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;
using System.Numerics;

namespace ExpenseSplitter.Domain.Services;

public static class SettlementCalculator
{
    private static readonly BigInteger MaximumDecimalCoefficient = new(decimal.MaxValue);

    public static IReadOnlyCollection<SettlementTransfer> Calculate(Trip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var balances = ParticipantBalanceCalculator.Calculate(trip);

        var creditors = balances.Where(balance => balance.AmountInCents > 0).ToArray();
        var debtors = balances.Where(balance => balance.AmountInCents < 0).ToArray();
        var transfers = new List<SettlementTransfer>();
        var creditorIndex = 0;
        var debtorIndex = 0;

        while (creditorIndex < creditors.Length && debtorIndex < debtors.Length)
        {
            var creditor = creditors[creditorIndex];
            var debtor = debtors[debtorIndex];
            var transferAmountInCents = BigInteger.Min(
                -debtor.AmountInCents,
                creditor.AmountInCents);

            AddTransfers(
                transfers,
                debtor.ParticipantId,
                creditor.ParticipantId,
                transferAmountInCents);

            debtor.AmountInCents += transferAmountInCents;
            creditor.AmountInCents -= transferAmountInCents;

            if (debtor.AmountInCents == 0)
            {
                debtorIndex++;
            }

            if (creditor.AmountInCents == 0)
            {
                creditorIndex++;
            }
        }

        if (creditors.Any(creditor => creditor.AmountInCents != 0)
            || debtors.Any(debtor => debtor.AmountInCents != 0))
        {
            throw new InvalidOperationException("Settlement did not clear all participant balances.");
        }

        return transfers.AsReadOnly();
    }

    private static void AddTransfers(
        ICollection<SettlementTransfer> transfers,
        Guid fromParticipantId,
        Guid toParticipantId,
        BigInteger amountInCents)
    {
        while (amountInCents > 0)
        {
            if (TryConvertExactly(amountInCents, out var amount))
            {
                transfers.Add(new SettlementTransfer(
                    fromParticipantId,
                    toParticipantId,
                    amount));
                return;
            }

            var wholeUnits = BigInteger.Min(
                amountInCents / 100,
                MaximumDecimalCoefficient);
            var chunkInCents = wholeUnits * 100;

            transfers.Add(new SettlementTransfer(
                fromParticipantId,
                toParticipantId,
                CreateDecimal(wholeUnits, 0)));
            amountInCents -= chunkInCents;
        }
    }

    private static bool TryConvertExactly(BigInteger amountInCents, out decimal amount)
    {
        if (amountInCents <= MaximumDecimalCoefficient)
        {
            amount = CreateDecimal(amountInCents, 2);
            return true;
        }

        if (amountInCents % 10 == 0
            && amountInCents / 10 <= MaximumDecimalCoefficient)
        {
            amount = CreateDecimal(amountInCents / 10, 1);
            return true;
        }

        if (amountInCents % 100 == 0
            && amountInCents / 100 <= MaximumDecimalCoefficient)
        {
            amount = CreateDecimal(amountInCents / 100, 0);
            return true;
        }

        amount = default;
        return false;
    }

    private static decimal CreateDecimal(BigInteger coefficient, byte scale)
    {
        var low = (int)(uint)(coefficient & uint.MaxValue);
        var middle = (int)(uint)((coefficient >> 32) & uint.MaxValue);
        var high = (int)(uint)((coefficient >> 64) & uint.MaxValue);
        return new decimal(low, middle, high, false, scale);
    }
}
