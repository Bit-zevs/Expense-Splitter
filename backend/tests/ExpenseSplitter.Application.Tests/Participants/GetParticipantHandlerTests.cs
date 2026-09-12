using ExpenseSplitter.Application.Participants.GetParticipant;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Participants;

public sealed class GetParticipantHandlerTests
{
    [Fact]
    public async Task ReturnsParticipantFromRequestedTrip()
    {
        var tripId = Guid.NewGuid();
        var trip = TestTrips.Create("Trip");
        var participant = trip.AddParticipant("Alice");
        var store = new StubTripStore(trip);
        var handler = new GetParticipantHandler(store, new TestCurrentAccount());
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            tripId,
            participant.Id,
            cancellation.Token);

        Assert.NotNull(result);
        Assert.Equal(participant.Id, result.Id);
        Assert.Equal(participant.Name, result.Name);
        Assert.Equal(tripId, store.RequestedTripId);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ReturnsNullWhenParticipantDoesNotExist()
    {
        var handler = new GetParticipantHandler(new StubTripStore(null), new TestCurrentAccount());

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubTripStore(Trip? trip) : TripStoreStub
    {
        public Guid RequestedTripId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public override Task<Trip?> FindWithParticipantsByIdAsync(
            Guid tripId,
            Guid accountId,
            CancellationToken cancellationToken)
        {
            RequestedTripId = tripId;
            CancellationToken = cancellationToken;
            return Task.FromResult(trip);
        }
    }
}
