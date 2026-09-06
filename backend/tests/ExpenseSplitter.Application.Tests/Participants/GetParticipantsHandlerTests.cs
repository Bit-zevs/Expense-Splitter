using ExpenseSplitter.Application.Participants.GetParticipants;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Participants;

public sealed class GetParticipantsHandlerTests
{
    [Fact]
    public async Task ReturnsTripParticipants()
    {
        var trip = new Trip("Summer vacation");
        var alice = trip.AddParticipant("Alice");
        var bob = trip.AddParticipant("Bob");
        var store = new StubTripStore(trip);
        var handler = new GetParticipantsHandler(store);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(trip.Id, cancellation.Token);

        Assert.NotNull(result);
        Assert.Equal(
            new[] { (alice.Id, alice.Name), (bob.Id, bob.Name) },
            result.Select(participant => (participant.Id, participant.Name)));
        Assert.Equal(trip.Id, store.RequestedId);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ReturnsEmptyCollectionWhenTripHasNoParticipants()
    {
        var store = new StubTripStore(new Trip("Summer vacation"));
        var handler = new GetParticipantsHandler(store);

        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReturnsNullWhenTripDoesNotExist()
    {
        var store = new StubTripStore(null);
        var handler = new GetParticipantsHandler(store);

        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubTripStore(Trip? trip) : TripStoreStub
    {
        public Guid RequestedId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public override Task<Trip?> FindWithParticipantsByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            RequestedId = id;
            CancellationToken = cancellationToken;
            return Task.FromResult(trip);
        }

    }
}
