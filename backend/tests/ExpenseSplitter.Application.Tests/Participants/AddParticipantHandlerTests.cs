using ExpenseSplitter.Application.Participants.AddParticipant;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Participants;

public sealed class AddParticipantHandlerTests
{
    [Fact]
    public async Task AddsParticipantAndSavesTrip()
    {
        var trip = new Trip("Summer vacation");
        var store = new StubTripStore(trip);
        var handler = new AddParticipantHandler(store);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            trip.Id,
            "  Alice  ",
            cancellation.Token);

        Assert.NotNull(result);
        Assert.Equal("Alice", result.Name);
        Assert.Equal(result.Id, Assert.Single(trip.Participants).Id);
        Assert.Equal(trip.Id, store.RequestedId);
        Assert.Equal(1, store.SaveCount);
        Assert.All(store.CancellationTokens, token => Assert.Equal(cancellation.Token, token));
    }

    [Fact]
    public async Task ReturnsNullWithoutSavingWhenTripDoesNotExist()
    {
        var store = new StubTripStore(null);
        var handler = new AddParticipantHandler(store);

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            "Alice",
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, store.SaveCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectsMissingNameWithoutSaving(string? name)
    {
        var store = new StubTripStore(new Trip("Summer vacation"));
        var handler = new AddParticipantHandler(store);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            handler.HandleAsync(Guid.NewGuid(), name, CancellationToken.None));

        Assert.Equal(0, store.LoadCount);
        Assert.Equal(0, store.SaveCount);
    }

    private sealed class StubTripStore(Trip? trip) : TripStoreStub
    {
        public Guid RequestedId { get; private set; }

        public int SaveCount { get; private set; }

        public int LoadCount { get; private set; }

        public List<CancellationToken> CancellationTokens { get; } = [];

        public override Task<Trip?> FindTrackedAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            RequestedId = id;
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult(trip);
        }

        public override Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            CancellationTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }
    }
}
