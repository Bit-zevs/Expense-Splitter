using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.Enums;
using ExpenseSplitter.Domain.ValueObjects;
using Xunit;

namespace ExpenseSplitter.Domain.Tests.Services;

public sealed class EqualSplitTests
{
    [Fact]
    public void SplitsAmongAllCurrentMembers()
    {
        var (trip, participants) = MakeTrip(3);

        var expense = trip.AddEqualExpenseForAll(90m, "  Dinner  ", participants[0].Id);

        Assert.Equal(3, expense.Shares.Count);
        Assert.All(expense.Shares, share => Assert.Equal(30m, share.Amount));
        Assert.Equal(90m, expense.Amount);
        Assert.Equal("Dinner", expense.Description);
        Assert.Equal(SplitType.Equal, expense.SplitType);
        Assert.Same(expense, Assert.Single(trip.Expenses));
    }

    [Fact]
    public void SplitsOnlyAmongSelectedMembersAndPayerMayBeExcluded()
    {
        var (trip, participants) = MakeTrip(3);
        var payer = participants[0];
        var selected = participants.Skip(1).ToArray();

        var expense = trip.AddEqualExpense(
            100m,
            "Tickets",
            payer.Id,
            selected.Select(participant => participant.Id));

        Assert.Equal(
            selected.Select(participant => participant.Id).Order(),
            expense.Shares.Select(share => share.ParticipantId));
        Assert.All(expense.Shares, share => Assert.Equal(50m, share.Amount));
        Assert.Equal(payer.Id, expense.PaidByParticipantId);
        Assert.DoesNotContain(payer.Id, expense.ParticipantIds);
    }

    [Fact]
    public void AllocatesRemainderInStableIdOrder()
    {
        var (firstTrip, firstParticipants) = MakeTrip(3);
        var firstSelection = firstParticipants.Select(participant => participant.Id).Reverse().ToArray();
        var first = firstTrip.AddEqualExpense(100m, "Dinner", firstParticipants[0].Id, firstSelection);

        var orderedShares = first.Shares.OrderBy(share => share.ParticipantId).ToArray();
        Assert.Equal(new[] { 33.34m, 33.33m, 33.33m }, orderedShares.Select(share => share.Amount));

        var second = firstTrip.AddEqualExpense(
            100m,
            "Dinner again",
            firstParticipants[0].Id,
            firstSelection.Reverse());
        Assert.Equal(
            first.Shares.Select(share => (share.ParticipantId, share.Amount)),
            second.Shares.Select(share => (share.ParticipantId, share.Amount)));
    }

    [Fact]
    public void AmountSmallerThanParticipantCountAllowsZeroShares()
    {
        var (trip, participants) = MakeTrip(3);

        var expense = trip.AddEqualExpenseForAll(0.02m, "Small expense", participants[0].Id);

        Assert.Equal(new[] { 0.01m, 0.01m, 0m }, expense.Shares.Select(share => share.Amount));
    }

    [Fact]
    public void SingleParticipantReceivesWholeAmount()
    {
        var (trip, participants) = MakeTrip(2);

        var expense = trip.AddEqualExpense(
            12.34m,
            "Coffee",
            participants[0].Id,
            new[] { participants[1].Id });

        var share = Assert.Single(expense.Shares);
        Assert.Equal(participants[1].Id, share.ParticipantId);
        Assert.Equal(12.34m, share.Amount);
    }

    [Fact]
    public void SharesAreSnapshotOfSelectionAndMembership()
    {
        var (trip, participants) = MakeTrip(2);
        var selection = participants.Select(participant => participant.Id).ToList();
        var expense = trip.AddEqualExpense(10m, "Lunch", participants[0].Id, selection);

        selection.Clear();
        trip.AddParticipant("New participant");

        Assert.Equal(participants.Select(participant => participant.Id).Order(), expense.ParticipantIds);
        Assert.Equal(10m, expense.Shares.Sum(share => share.Amount));
        Assert.Throws<NotSupportedException>(
            () => ((IList<ExpenseShare>)expense.Shares).Clear());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.001")]
    [InlineData("79228162514264337593543950335")]
    public void RejectsInvalidAmountsWithoutAddingExpense(string value)
    {
        var (trip, participants) = MakeTrip(1);
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            trip.AddEqualExpenseForAll(amount, "Dinner", participants[0].Id));
        Assert.Empty(trip.Expenses);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingDescriptionWithoutAddingExpense(string? description)
    {
        var (trip, participants) = MakeTrip(1);

        Assert.ThrowsAny<ArgumentException>(() =>
            trip.AddEqualExpenseForAll(10m, description!, participants[0].Id));
        Assert.Empty(trip.Expenses);
    }

    [Fact]
    public void RejectsInvalidSelectionAndPayerWithoutAddingExpense()
    {
        var (trip, participants) = MakeTrip(2);
        var payerId = participants[0].Id;
        var participantId = participants[1].Id;

        Assert.Throws<ArgumentException>(() =>
            trip.AddEqualExpense(10m, "Dinner", payerId, Array.Empty<Guid>()));
        Assert.Throws<ArgumentException>(() =>
            trip.AddEqualExpense(10m, "Dinner", payerId, new[] { participantId, participantId }));
        Assert.Throws<ArgumentException>(() =>
            trip.AddEqualExpense(10m, "Dinner", payerId, new[] { Guid.NewGuid() }));
        Assert.Throws<ArgumentException>(() =>
            trip.AddEqualExpense(10m, "Dinner", payerId, new[] { Guid.Empty }));
        Assert.Throws<ArgumentException>(() =>
            trip.AddEqualExpense(10m, "Dinner", Guid.NewGuid(), new[] { participantId }));
        Assert.Throws<ArgumentNullException>(() =>
            trip.AddEqualExpense(10m, "Dinner", payerId, null!));
        Assert.Empty(trip.Expenses);
    }

    [Fact]
    public void SplittingForAllRejectsTripWithoutParticipants()
    {
        var trip = new Trip("Empty trip");

        Assert.Throws<ArgumentException>(() =>
            trip.AddEqualExpenseForAll(10m, "Dinner", Guid.NewGuid()));
        Assert.Empty(trip.Expenses);
    }

    [Fact]
    public void PreservesTotalAndDifferenceNeverExceedsOneCent()
    {
        var (trip, participants) = MakeTrip(3);

        for (var cents = 1; cents <= 1000; cents++)
        {
            var amount = cents / 100m;
            var shares = trip.AddEqualExpenseForAll(amount, "Expense", participants[0].Id).Shares;
            Assert.Equal(amount, shares.Sum(share => share.Amount));
            Assert.InRange(shares.Max(share => share.Amount) - shares.Min(share => share.Amount), 0m, 0.01m);
            Assert.All(shares, share => Assert.Equal(0m, share.Amount % 0.01m));
        }
    }

    [Theory]
    [InlineData("792281625142643375935439503.35", "0")]
    [InlineData("792281625142643375935439503.34", "0.01")]
    public void LargeAmountPreservesTotal(string value, string difference)
    {
        var (trip, participants) = MakeTrip(3);
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var expectedDifference = decimal.Parse(difference, System.Globalization.CultureInfo.InvariantCulture);

        var expense = trip.AddEqualExpenseForAll(amount, "Large expense", participants[0].Id);

        Assert.Equal(amount, expense.Shares.Sum(share => share.Amount));
        Assert.Equal(
            expectedDifference,
            expense.Shares.Max(share => share.Amount) - expense.Shares.Min(share => share.Amount));
    }

    private static (Trip Trip, Participant[] Participants) MakeTrip(int participantCount)
    {
        var trip = new Trip("Trip");
        var participants = Enumerable.Range(1, participantCount)
            .Select(index => trip.AddParticipant($"Participant {index}"))
            .ToArray();
        return (trip, participants);
    }
}
