using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.Services;
using ExpenseSplitter.Domain.ValueObjects;
using Xunit;

namespace ExpenseSplitter.Domain.Tests.Services;

public sealed class SettlementCalculatorTests
{
    [Fact]
    public void UsesParticipantIdOrderRatherThanLargestBalanceOrder()
    {
        var trip = TestTrips.Create("Trip");
        for (var i = 0; i < 4; i++) trip.AddParticipant($"Person {i}");
        var ids = trip.Participants.OrderBy(p => p.Id).Select(p => p.Id).ToArray();
        trip.AddEqualExpense(4m, "First", ids[0], [ids[2]]);
        trip.AddEqualExpense(2m, "Second", ids[0], [ids[3]]);
        trip.AddEqualExpense(4m, "Third", ids[1], [ids[3]]);
        Assert.Equal(new[] { (ids[2], ids[0], 4m), (ids[3], ids[0], 2m), (ids[3], ids[1], 4m) },
            SettlementCalculator.Calculate(trip).Select(t => (t.FromParticipantId, t.ToParticipantId, t.Amount)));
    }

    [Fact]
    public void CancelsOpposingExpensesBeforeAccumulationBeyondDecimalMagnitude()
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        for (var i = 0; i < 101; i++) trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Credit", payer.Id, [debtor.Id]);
        for (var i = 0; i < 100; i++) trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Debit", debtor.Id, [payer.Id]);
        Assert.Equal(MoneyLimits.MaximumAmount, Assert.Single(SettlementCalculator.Calculate(trip)).Amount);
    }
    [Fact]
    public void CalculatesExpectedTransfersForOneCreditorAndTwoDebtors()
    {
        var trip = TestTrips.Create("Trip");
        var ivan = trip.AddParticipant("Ivan");
        var oleg = trip.AddParticipant("Oleg");
        var masha = trip.AddParticipant("Masha");
        trip.AddEqualExpense(20m, "Oleg's part", ivan.Id, new[] { ivan.Id, oleg.Id });
        trip.AddEqualExpense(60m, "Masha's part", ivan.Id, new[] { ivan.Id, masha.Id });

        var transfers = SettlementCalculator.Calculate(trip);

        Assert.Equal(
            new[]
            {
                new ExpectedTransfer(oleg.Id, ivan.Id, 10m),
                new ExpectedTransfer(masha.Id, ivan.Id, 30m)
            }.OrderBy(transfer => transfer.FromParticipantId),
            transfers.Select(transfer => new ExpectedTransfer(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.Amount)));
    }

    [Fact]
    public void MatchesSeveralDebtorsAndCreditorsUntilAllBalancesAreCleared()
    {
        var trip = TestTrips.Create("Trip");
        var participants = Enumerable.Range(1, 4)
            .Select(index => trip.AddParticipant($"Participant {index}"))
            .OrderBy(participant => participant.Id)
            .ToArray();
        var alice = participants[0];
        var bob = participants[1];
        var charlie = participants[2];
        var diana = participants[3];
        trip.AddEqualExpense(60m, "Alice paid", alice.Id, new[] { alice.Id, bob.Id });
        trip.AddEqualExpense(40m, "Charlie paid", charlie.Id, new[] { charlie.Id, diana.Id });

        var transfers = SettlementCalculator.Calculate(trip);

        Assert.Equal(
            new[]
            {
                new ExpectedTransfer(bob.Id, alice.Id, 30m),
                new ExpectedTransfer(diana.Id, charlie.Id, 20m)
            },
            transfers.Select(transfer => new ExpectedTransfer(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.Amount)));
    }

    [Fact]
    public void SplitsOneDebtorsPaymentBetweenSeveralCreditors()
    {
        var trip = TestTrips.Create("Trip");
        var alice = trip.AddParticipant("Alice");
        var bob = trip.AddParticipant("Bob");
        var charlie = trip.AddParticipant("Charlie");
        trip.AddEqualExpense(20m, "Alice paid for Bob", alice.Id, new[] { bob.Id });
        trip.AddEqualExpense(30m, "Charlie paid for Bob", charlie.Id, new[] { bob.Id });

        var transfers = SettlementCalculator.Calculate(trip);

        Assert.Equal(
            new[]
            {
                new ExpectedTransfer(bob.Id, alice.Id, 20m),
                new ExpectedTransfer(bob.Id, charlie.Id, 30m)
            }.OrderBy(transfer => transfer.ToParticipantId),
            transfers.Select(transfer => new ExpectedTransfer(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.Amount)));
    }

    [Fact]
    public void SupportsPayerWhoIsNotIncludedInExpenseShares()
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        trip.AddEqualExpense(25.01m, "Ticket", payer.Id, new[] { debtor.Id });

        var transfer = Assert.Single(SettlementCalculator.Calculate(trip));

        Assert.Equal(debtor.Id, transfer.FromParticipantId);
        Assert.Equal(payer.Id, transfer.ToParticipantId);
        Assert.Equal(25.01m, transfer.Amount);
    }

    [Fact]
    public void SupportsMaximumExactCentBalance()
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Expense", payer.Id, new[] { debtor.Id });

        var transfer = Assert.Single(SettlementCalculator.Calculate(trip));

        Assert.Equal(MoneyLimits.MaximumAmount, transfer.Amount);
        Assert.Equal(debtor.Id, transfer.FromParticipantId);
        Assert.Equal(payer.Id, transfer.ToParticipantId);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(100)]
    public void SupportsExactDerivedValuesAboveSingleExpenseLimit(int expenseCount)
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        for (var index = 0; index < expenseCount; index++)
            trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Expense", payer.Id, [debtor.Id]);

        var expected = MoneyLimits.MaximumAmount * expenseCount;
        var balances = ParticipantBalanceCalculator.Calculate(trip);
        Assert.Equal(expected, balances.Single(b => b.ParticipantId == payer.Id).Amount);
        Assert.Equal(-expected, balances.Single(b => b.ParticipantId == debtor.Id).Amount);
        Assert.Equal(expected, Assert.Single(SettlementCalculator.Calculate(trip)).Amount);
    }

    [Theory]
    [InlineData(3)] // Within decimal's magnitude, but the exact cents cannot be represented.
    [InlineData(101)] // Exceeds decimal.MaxValue itself.
    public void RejectsDerivedValuesThatDecimalCannotRepresentExactly(int expenseCount)
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        for (var index = 0; index < expenseCount; index++)
            trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Expense", payer.Id, [debtor.Id]);

        Assert.Throws<OverflowException>(() => SettlementCalculator.Calculate(trip));
    }

    [Fact]
    public void SettlementKeepsExactCentsInIntermediateRemaindersAboveExpenseLimit()
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        for (var index = 0; index < 100; index++)
        {
            var debtor = trip.AddParticipant($"Debtor {index}");
            trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Expense", payer.Id, [debtor.Id]);
        }

        var balances = ParticipantBalanceCalculator.Calculate(trip);
        Assert.Equal(decimal.MaxValue, balances.Single(b => b.ParticipantId == payer.Id).Amount);
        var transfers = SettlementCalculator.CalculateFromBalances(balances);
        Assert.Equal(100, transfers.Count);
        Assert.All(transfers, transfer => Assert.Equal(MoneyLimits.MaximumAmount, transfer.Amount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalBalanceIsIndependentOfIntermediateOverflow(bool counterExpenseFirst)
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        if (counterExpenseFirst)
            trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Counter", debtor.Id, [payer.Id]);
        trip.AddEqualExpense(MoneyLimits.MaximumAmount, "First", payer.Id, [debtor.Id]);
        trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Second", payer.Id, [debtor.Id]);
        if (!counterExpenseFirst)
            trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Counter", debtor.Id, [payer.Id]);

        var transfer = Assert.Single(SettlementCalculator.Calculate(trip));
        Assert.Equal(MoneyLimits.MaximumAmount, transfer.Amount);
        Assert.Equal(payer.Id, transfer.ToParticipantId);
    }

    [Fact]
    public void AllowsExpenseWithZeroNetChangeAtMaximumBalance()
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");

        trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Expense", payer.Id, new[] { debtor.Id });
        trip.AddEqualExpense(
            MoneyLimits.MaximumAmount,
            "Personal expense",
            payer.Id,
            new[] { payer.Id });

        var transfer = Assert.Single(SettlementCalculator.Calculate(trip));
        Assert.Equal(MoneyLimits.MaximumAmount, transfer.Amount);
    }

    [Fact]
    public void PreservesCentWhenMaximumExpenseHasSmallCounterExpense()
    {
        var trip = TestTrips.Create("Trip");
        var alice = trip.AddParticipant("Alice");
        var bob = trip.AddParticipant("Bob");
        trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Large expense", alice.Id, [bob.Id]);
        trip.AddEqualExpense(0.01m, "Counter expense", bob.Id, [alice.Id]);

        var transfer = Assert.Single(SettlementCalculator.Calculate(trip));

        Assert.Equal(MoneyLimits.MaximumAmount - 0.01m, transfer.Amount);
        Assert.Equal(bob.Id, transfer.FromParticipantId);
        Assert.Equal(alice.Id, transfer.ToParticipantId);
    }

    [Fact]
    public void ReturnsNoTransfersWhenThereAreNoExpenses()
    {
        var trip = TestTrips.Create("Trip");
        trip.AddParticipant("Alice");

        var transfers = SettlementCalculator.Calculate(trip);

        Assert.Empty(transfers);
        Assert.Throws<NotSupportedException>(
            () => ((IList<SettlementTransfer>)transfers).Clear());
    }

    [Fact]
    public void ReturnsNoTransfersWhenParticipantPaidOnlyForThemselves()
    {
        var trip = TestTrips.Create("Trip");
        var participant = trip.AddParticipant("Alice");
        trip.AddEqualExpense(
            12.34m,
            "Personal expense",
            participant.Id,
            new[] { participant.Id });

        var transfers = SettlementCalculator.Calculate(trip);

        Assert.Empty(transfers);
    }

    [Fact]
    public void DoesNotChangeTripAndReturnsSameResultWhenCalculatedAgain()
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        trip.AddEqualExpense(10m, "Expense", payer.Id, new[] { debtor.Id });

        var first = SettlementCalculator.Calculate(trip);
        var second = SettlementCalculator.Calculate(trip);

        Assert.Equal(first, second);
        Assert.Single(trip.Expenses);
    }

    [Fact]
    public void CalculatesFromExistingBalancesWithoutChangingThem()
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        trip.AddEqualExpense(10m, "Expense", payer.Id, new[] { debtor.Id });
        var balances = ParticipantBalanceCalculator.Calculate(trip);
        var expectedAmounts = balances.Select(balance => balance.Amount).ToArray();

        var transfers = SettlementCalculator.CalculateFromBalances(balances);

        Assert.Single(transfers);
        Assert.Equal(expectedAmounts, balances.Select(balance => balance.Amount));
    }

    [Fact]
    public void RejectsNullTrip()
    {
        Assert.Throws<ArgumentNullException>(() => SettlementCalculator.Calculate(null!));
    }

    private sealed record ExpectedTransfer(
        Guid FromParticipantId,
        Guid ToParticipantId,
        decimal Amount);

}
