using ExpenseSplitter.Application.Trips.CreateTrip;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Trips;

public sealed class CreateTripHandlerTests
{
    [Fact]
    public async Task CreatesAndPersistsTrip()
    {
        var store = new RecordingTripStore();
        var handler = new CreateTripHandler(store, new TestCurrentAccount());
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new CreateTripCommand("  Summer vacation  ", "EUR"),
            cancellation.Token);

        var trip = Assert.IsType<Trip>(store.Trip);
        Assert.Equal(trip.Id, result.Id);
        Assert.Equal("Summer vacation", result.Name);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal("EUR", trip.Currency);
        Assert.Equal(trip.CreatedAt, result.CreatedAt);
        Assert.Equal(cancellation.Token, store.CancellationToken);
        Assert.Equal(cancellation.Token, store.SaveCancellationToken);
        Assert.True(store.Saved);
    }

    [Fact]
    public async Task UsesRubWhenCurrencyIsOmitted()
    {
        var store = new RecordingTripStore();
        var handler = new CreateTripHandler(store, new TestCurrentAccount());

        var result = await handler.HandleAsync(
            new CreateTripCommand("Trip"),
            CancellationToken.None);

        Assert.Equal("RUB", result.Currency);
    }

    [Fact]
    public async Task RejectsUnsupportedCurrencyWithoutPersisting()
    {
        var store = new RecordingTripStore();
        var handler = new CreateTripHandler(store, new TestCurrentAccount());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(
                new CreateTripCommand("Trip", "GBP"),
                CancellationToken.None));

        Assert.Null(store.Trip);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectsMissingNameWithoutPersisting(string? name)
    {
        var store = new RecordingTripStore();
        var handler = new CreateTripHandler(store, new TestCurrentAccount());

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            handler.HandleAsync(new CreateTripCommand(name), CancellationToken.None));

        Assert.Null(store.Trip);
    }

    private sealed class RecordingTripStore : TripStoreStub
    {
        public Trip? Trip { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public CancellationToken SaveCancellationToken { get; private set; }
        public bool Saved { get; private set; }

        public override Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Assert.NotNull(Trip);
            Saved = true;
            SaveCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public override Task AddAsync(Trip trip, CancellationToken cancellationToken)
        {
            Trip = trip;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

    }
}
