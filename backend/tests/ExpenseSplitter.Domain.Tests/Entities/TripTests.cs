using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Domain.Tests.Entities;

public sealed class TripTests
{
    [Fact]
    public void ConstructorCreatesValidTripAndTrimsName()
    {
        var beforeCreation = DateTimeOffset.UtcNow;

        var trip = new Trip("  Summer vacation  ");

        Assert.NotEqual(Guid.Empty, trip.Id);
        Assert.Equal("Summer vacation", trip.Name);
        Assert.Equal("RUB", trip.Currency);
        Assert.InRange(trip.CreatedAt, beforeCreation, DateTimeOffset.UtcNow);
        Assert.Equal(0, trip.CreatedAt.Ticks % 10);
        Assert.Empty(trip.Participants);
        Assert.Empty(trip.Expenses);
    }

    [Theory]
    [InlineData("RUB")]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData(" eur ")]
    public void ConstructorAcceptsSupportedCurrency(string currency)
    {
        var trip = new Trip("Trip", currency);

        Assert.Equal(currency.Trim().ToUpperInvariant(), trip.Currency);
    }

    [Theory]
    [InlineData("")]
    [InlineData("GBP")]
    public void ConstructorRejectsUnsupportedCurrency(string currency)
    {
        var error = Assert.Throws<ArgumentException>(() => new Trip("Trip", currency));

        Assert.Equal("currency", error.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsMissingName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Trip(name!));
    }

    [Fact]
    public void AddParticipantCreatesParticipantAndAddsItToTrip()
    {
        var trip = new Trip("Trip");

        var participant = trip.AddParticipant("  Alice  ");

        Assert.NotEqual(Guid.Empty, participant.Id);
        Assert.Equal("Alice", participant.Name);
        Assert.Same(participant, Assert.Single(trip.Participants));
    }

    [Fact]
    public void ExpenseOccurrenceTimeUsesMinutePrecision()
    {
        var trip = new Trip("Trip");
        var participant = trip.AddParticipant("Alice");

        var expense = trip.AddEqualExpenseForAll(12.34m, "Coffee", participant.Id);

        Assert.Equal(0, expense.OccurredAt.Second);
        Assert.Equal(0, expense.OccurredAt.Millisecond);
        Assert.Equal(TimeSpan.Zero, expense.OccurredAt.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddParticipantRejectsMissingName(string? name)
    {
        var trip = new Trip("Trip");

        Assert.ThrowsAny<ArgumentException>(() => trip.AddParticipant(name!));
        Assert.Empty(trip.Participants);
    }

    [Fact]
    public void PublicCollectionsCannotBeModifiedDirectly()
    {
        var trip = new Trip("Trip");
        trip.AddParticipant("Alice");

        Assert.Throws<NotSupportedException>(
            () => ((IList<Participant>)trip.Participants).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<Expense>)trip.Expenses).Clear());
    }

    [Fact]
    public void ExpenseOccurrenceTimeCanBeSetAndIsNormalizedToUtcMinute()
    {
        var trip = new Trip("Trip");
        var participant = trip.AddParticipant("Alice");
        var localTime = new DateTimeOffset(2026, 9, 8, 14, 37, 42, TimeSpan.FromHours(5));

        var expense = trip.AddEqualExpenseForAll(12.34m, "Coffee", participant.Id, localTime);

        Assert.Equal(new DateTimeOffset(2026, 9, 8, 9, 37, 0, TimeSpan.Zero), expense.OccurredAt);
    }

    [Fact]
    public void RemoveExpenseRemovesOnlyRequestedExpense()
    {
        var trip = new Trip("Trip");
        var participant = trip.AddParticipant("Alice");
        var removed = trip.AddEqualExpenseForAll(10m, "Removed", participant.Id);
        var kept = trip.AddEqualExpenseForAll(20m, "Kept", participant.Id);

        Assert.True(trip.RemoveExpense(removed.Id));

        Assert.Equal(kept.Id, Assert.Single(trip.Expenses).Id);
        Assert.False(trip.RemoveExpense(removed.Id));
    }

    [Fact]
    public void RemoveParticipantAlsoRemovesExpensesInvolvingThem()
    {
        var trip = new Trip("Trip");
        var alice = trip.AddParticipant("Alice");
        var bob = trip.AddParticipant("Bob");
        var charlie = trip.AddParticipant("Charlie");
        trip.AddEqualExpense(10m, "Alice paid", alice.Id, [bob.Id]);
        trip.AddEqualExpense(20m, "Bob share", charlie.Id, [bob.Id]);
        var kept = trip.AddEqualExpense(30m, "Unrelated", charlie.Id, [alice.Id]);

        Assert.True(trip.RemoveParticipant(bob.Id));

        Assert.DoesNotContain(trip.Participants, participant => participant.Id == bob.Id);
        Assert.Equal(kept.Id, Assert.Single(trip.Expenses).Id);
        Assert.False(trip.RemoveParticipant(bob.Id));
    }
}
