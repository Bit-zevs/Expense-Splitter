using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.Services;
using ExpenseSplitter.Domain.ValueObjects;
using Xunit;

namespace ExpenseSplitter.Domain.Tests.Services;

public sealed class SettlementCalculatorTests
{
    [Fact]
    public void CalculatesExpectedTransfersForOneCreditorAndTwoDebtors()
    {
        var trip = new Trip("Trip");
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
            },
            transfers.Select(transfer => new ExpectedTransfer(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.Amount)));
    }

    [Fact]
    public void MatchesSeveralDebtorsAndCreditorsUntilAllBalancesAreCleared()
    {
        var trip = new Trip("Trip");
        var alice = trip.AddParticipant("Alice");
        var bob = trip.AddParticipant("Bob");
        var charlie = trip.AddParticipant("Charlie");
        var diana = trip.AddParticipant("Diana");
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
        var trip = new Trip("Trip");
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
            },
            transfers.Select(transfer => new ExpectedTransfer(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.Amount)));
    }

    [Fact]
    public void SupportsPayerWhoIsNotIncludedInExpenseShares()
    {
        var trip = new Trip("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        trip.AddEqualExpense(25.01m, "Ticket", payer.Id, new[] { debtor.Id });

        var transfer = Assert.Single(SettlementCalculator.Calculate(trip));

        Assert.Equal(debtor.Id, transfer.FromParticipantId);
        Assert.Equal(payer.Id, transfer.ToParticipantId);
        Assert.Equal(25.01m, transfer.Amount);
    }

    [Fact]
    public void ReturnsNoTransfersWhenThereAreNoExpenses()
    {
        var trip = new Trip("Trip");
        trip.AddParticipant("Alice");

        var transfers = SettlementCalculator.Calculate(trip);

        Assert.Empty(transfers);
        Assert.Throws<NotSupportedException>(
            () => ((IList<SettlementTransfer>)transfers).Clear());
    }

    [Fact]
    public void ReturnsNoTransfersWhenParticipantPaidOnlyForThemselves()
    {
        var trip = new Trip("Trip");
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
        var trip = new Trip("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        trip.AddEqualExpense(10m, "Expense", payer.Id, new[] { debtor.Id });

        var first = SettlementCalculator.Calculate(trip);
        var second = SettlementCalculator.Calculate(trip);

        Assert.Equal(first, second);
        Assert.Single(trip.Expenses);
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
