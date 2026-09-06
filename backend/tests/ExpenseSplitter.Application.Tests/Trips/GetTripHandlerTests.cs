using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Application.Trips.GetTrip;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Trips;

public sealed class GetTripHandlerTests
{
    [Fact]
    public async Task ReturnsTripWhenItExists()
    {
        var trip = new Trip("Summer vacation");
        var store = new StubTripStore(trip);
        var handler = new GetTripHandler(store);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(trip.Id, cancellation.Token);

        Assert.NotNull(result);
        Assert.Equal(trip.Id, result.Id);
        Assert.Equal(trip.Name, result.Name);
        Assert.Equal(trip.CreatedAt, result.CreatedAt);
        Assert.Equal(trip.Id, store.RequestedId);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ReturnsNullWhenTripDoesNotExist()
    {
        var store = new StubTripStore(null);
        var handler = new GetTripHandler(store);

        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubTripStore(Trip? trip) : ITripStore
    {
        public Guid RequestedId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task AddAsync(Trip tripToAdd, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            RequestedId = id;
            CancellationToken = cancellationToken;
            return Task.FromResult(trip);
        }

        public Task<Trip?> FindAggregateByIdAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult<Trip?>(null);

        public Task<Trip?> FindAggregateForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<Trip?>(null);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
