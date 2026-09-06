using ExpenseSplitter.Application.Trips;
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
        var handler = new CreateTripHandler(store);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new CreateTripCommand("  Summer vacation  "),
            cancellation.Token);

        var trip = Assert.IsType<Trip>(store.Trip);
        Assert.Equal(trip.Id, result.Id);
        Assert.Equal("Summer vacation", result.Name);
        Assert.Equal(trip.CreatedAt, result.CreatedAt);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectsMissingNameWithoutPersisting(string? name)
    {
        var store = new RecordingTripStore();
        var handler = new CreateTripHandler(store);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            handler.HandleAsync(new CreateTripCommand(name), CancellationToken.None));

        Assert.Null(store.Trip);
    }

    private sealed class RecordingTripStore : ITripStore
    {
        public Trip? Trip { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task AddAsync(Trip trip, CancellationToken cancellationToken)
        {
            Trip = trip;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Trip?>(null);

        public Task<Trip?> FindWithParticipantsByIdAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult<Trip?>(null);

        public Task<Trip?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Trip?>(null);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
