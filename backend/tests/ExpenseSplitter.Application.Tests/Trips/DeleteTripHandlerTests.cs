using ExpenseSplitter.Application.Trips.DeleteTrip;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Trips;

public sealed class DeleteTripHandlerTests
{
    [Fact]
    public async Task RemovesExistingTripAndSaves()
    {
        var trip = new Trip("Trip");
        var store = new StubTripStore(trip);

        var deleted = await new DeleteTripHandler(store)
            .HandleAsync(trip.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Same(trip, store.Removed);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task ReturnsFalseForMissingTrip()
    {
        var store = new StubTripStore(null);

        Assert.False(await new DeleteTripHandler(store)
            .HandleAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(0, store.SaveCount);
    }

    private sealed class StubTripStore(Trip? trip) : TripStoreStub
    {
        public Trip? Removed { get; private set; }
        public int SaveCount { get; private set; }

        public override Task<Trip?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(trip);

        public override void Remove(Trip value) => Removed = value;

        public override Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
