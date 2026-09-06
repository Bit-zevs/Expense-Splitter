using ExpenseSplitter.Application.Expenses.CreateEqualExpense;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Expenses;

public sealed class CreateEqualExpenseHandlerTests
{
    [Fact]
    public async Task CreatesEqualExpenseForAllParticipantsAndSavesTrip()
    {
        var trip = new Trip("Summer vacation");
        var payer = trip.AddParticipant("Alice");
        trip.AddParticipant("Bob");
        trip.AddParticipant("Charlie");
        var store = new StubTripStore(trip);
        var handler = new CreateEqualExpenseHandler(store);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            trip.Id,
            new CreateEqualExpenseCommand(100m, "  Dinner  ", payer.Id, null),
            cancellation.Token);

        Assert.NotNull(result);
        Assert.Equal("Dinner", result.Description);
        Assert.Equal("equal", result.SplitType);
        Assert.Equal(3, result.Shares.Count);
        Assert.Equal(result.Amount, result.Shares.Sum(share => share.Amount));
        Assert.Equal(result.Id, Assert.Single(trip.Expenses).Id);
        Assert.Equal(trip.Id, store.RequestedId);
        Assert.Equal(1, store.SaveCount);
        Assert.All(store.CancellationTokens, token => Assert.Equal(cancellation.Token, token));
    }

    [Fact]
    public async Task CreatesEqualExpenseForSelectedParticipants()
    {
        var trip = new Trip("Summer vacation");
        var payer = trip.AddParticipant("Alice");
        var selected = trip.AddParticipant("Bob");
        var store = new StubTripStore(trip);
        var handler = new CreateEqualExpenseHandler(store);

        var result = await handler.HandleAsync(
            trip.Id,
            new CreateEqualExpenseCommand(12.34m, "Coffee", payer.Id, [selected.Id]),
            CancellationToken.None);

        Assert.NotNull(result);
        var share = Assert.Single(result.Shares);
        Assert.Equal(selected.Id, share.ParticipantId);
        Assert.Equal(12.34m, share.Amount);
    }

    [Fact]
    public async Task ReturnsNullWithoutSavingWhenTripDoesNotExist()
    {
        var store = new StubTripStore(null);
        var handler = new CreateEqualExpenseHandler(store);

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            new CreateEqualExpenseCommand(10m, "Dinner", Guid.NewGuid(), null),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task RejectsEmptySelectionWithoutSaving()
    {
        var trip = new Trip("Summer vacation");
        var payer = trip.AddParticipant("Alice");
        var store = new StubTripStore(trip);
        var handler = new CreateEqualExpenseHandler(store);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(
            trip.Id,
            new CreateEqualExpenseCommand(10m, "Dinner", payer.Id, []),
            CancellationToken.None));

        Assert.Empty(trip.Expenses);
        Assert.Equal(0, store.LoadCount);
        Assert.Equal(0, store.SaveCount);
    }

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public async Task RejectsInvalidCommandWithoutLoadingTrip(
        CreateEqualExpenseCommand command)
    {
        var store = new StubTripStore(new Trip("Summer vacation"));
        var handler = new CreateEqualExpenseHandler(store);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => handler.HandleAsync(
            Guid.NewGuid(),
            command,
            CancellationToken.None));

        Assert.Equal(0, store.LoadCount);
        Assert.Equal(0, store.SaveCount);
    }

    public static TheoryData<CreateEqualExpenseCommand> InvalidCommands
    {
        get
        {
            var participantId = Guid.NewGuid();
            return
            [
                new(0m, "Dinner", participantId, [participantId]),
                new(1.001m, "Dinner", participantId, [participantId]),
                new(decimal.MaxValue, "Dinner", participantId, [participantId]),
                new(10m, " ", participantId, [participantId]),
                new(10m, "Dinner", Guid.Empty, [participantId]),
                new(10m, "Dinner", participantId, [Guid.Empty]),
                new(10m, "Dinner", participantId, [participantId, participantId])
            ];
        }
    }

    private sealed class StubTripStore(Trip? trip) : TripStoreStub
    {
        public Guid RequestedId { get; private set; }

        public int SaveCount { get; private set; }

        public int LoadCount { get; private set; }

        public List<CancellationToken> CancellationTokens { get; } = [];

        public override Task<Trip?> FindWithParticipantsForUpdateAsync(
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
