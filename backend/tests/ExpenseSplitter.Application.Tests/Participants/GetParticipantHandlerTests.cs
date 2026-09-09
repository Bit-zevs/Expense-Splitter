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
        var participant = TestTrips.Create("Trip").AddParticipant("Alice");
        var store = new StubTripStore(participant);
        var handler = new GetParticipantHandler(store, new AllowTripAccess());
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            tripId,
            participant.Id,
            cancellation.Token);

        Assert.NotNull(result);
        Assert.Equal(participant.Id, result.Id);
        Assert.Equal(participant.Name, result.Name);
        Assert.Equal(tripId, store.RequestedTripId);
        Assert.Equal(participant.Id, store.RequestedParticipantId);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ReturnsNullWhenParticipantDoesNotExist()
    {
        var handler = new GetParticipantHandler(new StubTripStore(null), new AllowTripAccess());

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubTripStore(Participant? participant) : TripStoreStub
    {
        public Guid RequestedTripId { get; private set; }

        public Guid RequestedParticipantId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public override Task<Participant?> FindParticipantByIdAsync(
            Guid tripId,
            Guid participantId,
            CancellationToken cancellationToken)
        {
            RequestedTripId = tripId;
            RequestedParticipantId = participantId;
            CancellationToken = cancellationToken;
            return Task.FromResult(participant);
        }
    }
}
