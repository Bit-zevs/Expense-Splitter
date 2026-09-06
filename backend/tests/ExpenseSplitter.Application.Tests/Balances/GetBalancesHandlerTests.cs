using ExpenseSplitter.Application.Balances.GetBalances;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Balances;

public sealed class GetBalancesHandlerTests
{
    [Fact]
    public async Task ReturnsAllParticipantBalances()
    {
        var (trip, alice, bob, charlie) = CreateTripWithExpenses();
        var store = new StubTripStore(trip);
        var handler = new GetBalancesHandler(store);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(trip.Id, null, cancellation.Token);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        AssertBalance(result, alice.Id, "Alice", "40.00");
        AssertBalance(result, bob.Id, "Bob", "-10.00");
        AssertBalance(result, charlie.Id, "Charlie", "-30.00");
        Assert.Equal(trip.Id, store.RequestedId);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ReturnsOnlySelectedParticipantBalances()
    {
        var (trip, alice, bob, _) = CreateTripWithExpenses();
        var handler = new GetBalancesHandler(new StubTripStore(trip));

        var result = await handler.HandleAsync(
            trip.Id,
            [bob.Id, alice.Id],
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        AssertBalance(result, alice.Id, "Alice", "40.00");
        AssertBalance(result, bob.Id, "Bob", "-10.00");
    }

    [Fact]
    public async Task EmptySelectionReturnsAllParticipantBalances()
    {
        var (trip, _, _, _) = CreateTripWithExpenses();
        var handler = new GetBalancesHandler(new StubTripStore(trip));

        var result = await handler.HandleAsync(trip.Id, [], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task FormatsBalanceLargerThanDecimalWithoutLosingCents()
    {
        const decimal maximumExpense = 792281625142643375935439503.35m;
        var trip = new Trip("Extreme trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        for (var index = 0; index < 101; index++)
        {
            trip.AddEqualExpense(maximumExpense, $"Expense {index}", payer.Id, [debtor.Id]);
        }

        var handler = new GetBalancesHandler(new StubTripStore(trip));

        var result = await handler.HandleAsync(trip.Id, null, CancellationToken.None);

        Assert.NotNull(result);
        AssertBalance(result, payer.Id, "Payer", "80020444139406980969479389838.35");
        AssertBalance(result, debtor.Id, "Debtor", "-80020444139406980969479389838.35");
    }

    [Fact]
    public async Task RejectsDuplicateParticipantIds()
    {
        var (trip, alice, _, _) = CreateTripWithExpenses();
        var handler = new GetBalancesHandler(new StubTripStore(trip));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(
            trip.Id,
            [alice.Id, alice.Id],
            CancellationToken.None));
    }

    [Fact]
    public async Task RejectsParticipantFromAnotherTrip()
    {
        var (trip, _, _, _) = CreateTripWithExpenses();
        var handler = new GetBalancesHandler(new StubTripStore(trip));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(
            trip.Id,
            [Guid.NewGuid()],
            CancellationToken.None));
    }

    [Fact]
    public async Task ReturnsNullWhenTripDoesNotExist()
    {
        var handler = new GetBalancesHandler(new StubTripStore(null));

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            null,
            CancellationToken.None);

        Assert.Null(result);
    }

    private static (Trip Trip, Participant Alice, Participant Bob, Participant Charlie)
        CreateTripWithExpenses()
    {
        var trip = new Trip("Summer vacation");
        var alice = trip.AddParticipant("Alice");
        var bob = trip.AddParticipant("Bob");
        var charlie = trip.AddParticipant("Charlie");
        trip.AddEqualExpenseForAll(90m, "Dinner", alice.Id);
        trip.AddEqualExpense(20m, "Taxi", bob.Id, [alice.Id]);
        return (trip, alice, bob, charlie);
    }

    private static void AssertBalance(
        IEnumerable<ParticipantBalanceResult> balances,
        Guid participantId,
        string name,
        string amount)
    {
        var balance = Assert.Single(
            balances,
            candidate => candidate.ParticipantId == participantId);
        Assert.Equal(name, balance.Name);
        Assert.Equal(amount, balance.Amount);
    }

    private sealed class StubTripStore(Trip? trip) : TripStoreStub
    {
        public Guid RequestedId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public override Task<Trip?> FindWithParticipantsAndExpensesByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            RequestedId = id;
            CancellationToken = cancellationToken;
            return Task.FromResult(trip);
        }

    }
}
