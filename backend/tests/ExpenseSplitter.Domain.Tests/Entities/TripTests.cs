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
    public void ExpenseCreationTimeUsesDatabasePrecision()
    {
        var trip = new Trip("Trip");
        var participant = trip.AddParticipant("Alice");

        var expense = trip.AddEqualExpenseForAll(12.34m, "Coffee", participant.Id);

        Assert.Equal(0, expense.CreatedAt.Ticks % 10);
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
}
