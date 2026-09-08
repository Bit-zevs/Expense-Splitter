using ExpenseSplitter.Application.Participants.DeleteParticipant;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Participants;

public sealed class DeleteParticipantHandlerTests
{
    [Fact]
    public async Task RemovesParticipantThroughAggregateAndSaves()
    {
        var trip = new Trip("Trip");
        var participant = trip.AddParticipant("Alice");
        var store = new StubTripStore(trip);

        var deleted = await new DeleteParticipantHandler(store)
            .HandleAsync(trip.Id, participant.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(trip.Participants);
        Assert.Equal(1, store.SaveCount);
    }

    private sealed class StubTripStore(Trip trip) : TripStoreStub
    {
        public int SaveCount { get; private set; }

        public override Task<Trip?> FindWithParticipantsAndExpensesForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult<Trip?>(trip);

        public override Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
