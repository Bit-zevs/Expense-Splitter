using ExpenseSplitter.Application.Trips.DeleteTrip;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Trips;

public sealed class DeleteTripHandlerTests
{
    [Fact]
    public async Task RemovesExistingTripAndSaves()
    {
        var trip = TestTrips.Create("Trip");
        var store = new StubTripStore(trip);

        var deleted = await new DeleteTripHandler(store, new AllowTripAccess())
            .HandleAsync(trip.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Same(trip, store.Removed);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task ReturnsFalseForMissingTrip()
    {
        var store = new StubTripStore(null);

        Assert.False(await new DeleteTripHandler(store, new AllowTripAccess())
            .HandleAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(0, store.SaveCount);
    }

    private sealed class StubTripStore(Trip? trip) : TripStoreStub
    {
        public Trip? Removed { get; private set; }
        public int SaveCount { get; private set; }

        public override Task<Trip?> FindTrackedAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(trip);

        public override void Remove(Trip value) => Removed = value;

        public override Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
