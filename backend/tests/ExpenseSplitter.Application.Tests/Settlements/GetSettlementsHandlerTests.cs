using ExpenseSplitter.Application.Settlements.GetSettlements;
using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Settlements;

public sealed class GetSettlementsHandlerTests
{
    [Fact]
    public async Task ReturnsBalancesAndSettlementTransfers()
    {
        var trip = TestTrips.Create("Summer vacation");
        var alice = trip.AddParticipant("Alice");
        var bob = trip.AddParticipant("Bob");
        var charlie = trip.AddParticipant("Charlie");
        trip.AddEqualExpenseForAll(90m, "Dinner", alice.Id);
        trip.AddEqualExpense(20m, "Taxi", bob.Id, [alice.Id]);
        var store = new StubTripStore(trip);
        var handler = new GetSettlementsHandler(store, new TestCurrentAccount());
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(trip.Id, cancellation.Token);

        Assert.NotNull(result);
        AssertBalance(result.Balances, alice.Id, 40m);
        AssertBalance(result.Balances, bob.Id, -10m);
        AssertBalance(result.Balances, charlie.Id, -30m);
        AssertTransfer(result.Transfers, bob.Id, alice.Id, 10m);
        AssertTransfer(result.Transfers, charlie.Id, alice.Id, 30m);
        Assert.Equal(trip.Id, store.RequestedId);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ReturnsZeroBalancesAndNoTransfersWhenThereAreNoExpenses()
    {
        var trip = TestTrips.Create("Summer vacation");
        var alice = trip.AddParticipant("Alice");
        var handler = new GetSettlementsHandler(new StubTripStore(trip), new TestCurrentAccount());

        var result = await handler.HandleAsync(trip.Id, CancellationToken.None);

        Assert.NotNull(result);
        AssertBalance(result.Balances, alice.Id, 0m);
        Assert.Empty(result.Transfers);
    }

    [Fact]
    public async Task ReturnsNullWhenTripDoesNotExist()
    {
        var handler = new GetSettlementsHandler(new StubTripStore(null), new TestCurrentAccount());

        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsBalanceOutsideDecimalRange()
    {
        var trip = TestTrips.Create("Extreme trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        for (var index = 0; index < 101; index++)
            trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Expense", payer.Id, [debtor.Id]);

        var handler = new GetSettlementsHandler(new StubTripStore(trip), new TestCurrentAccount());

        await Assert.ThrowsAsync<OverflowException>(() =>
            handler.HandleAsync(trip.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ReturnsMaximumExactCentBalance()
    {
        var trip = TestTrips.Create("Large trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Expense", payer.Id, [debtor.Id]);
        var handler = new GetSettlementsHandler(new StubTripStore(trip), new TestCurrentAccount());

        var result = await handler.HandleAsync(trip.Id, CancellationToken.None);

        Assert.NotNull(result);
        AssertBalance(result.Balances, payer.Id, MoneyLimits.MaximumAmount);
        AssertBalance(result.Balances, debtor.Id, -MoneyLimits.MaximumAmount);
        AssertTransfer(result.Transfers, debtor.Id, payer.Id, MoneyLimits.MaximumAmount);
    }

    private static void AssertBalance(
        IEnumerable<SettlementBalanceResult> balances,
        Guid participantId,
        decimal amount)
    {
        var balance = Assert.Single(
            balances,
            candidate => candidate.ParticipantId == participantId);
        Assert.Equal(amount, balance.Balance);
    }

    private static void AssertTransfer(
        IEnumerable<SettlementTransferResult> transfers,
        Guid fromParticipantId,
        Guid toParticipantId,
        decimal amount)
    {
        var transfer = Assert.Single(
            transfers,
            candidate => candidate.FromParticipantId == fromParticipantId);
        Assert.Equal(toParticipantId, transfer.ToParticipantId);
        Assert.Equal(amount, transfer.Amount);
    }

    private sealed class StubTripStore(Trip? trip) : TripStoreStub
    {
        public Guid RequestedId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public override Task<Trip?> FindWithParticipantsAndExpensesByIdAsync(
            Guid id,
            Guid accountId,
            CancellationToken cancellationToken)
        {
            RequestedId = id;
            CancellationToken = cancellationToken;
            return Task.FromResult(trip);
        }
    }
}
