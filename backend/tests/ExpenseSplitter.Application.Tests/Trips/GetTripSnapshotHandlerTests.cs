using ExpenseSplitter.Application.Trips.GetTripSnapshot;
using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Trips;

public sealed class GetTripSnapshotHandlerTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)]
    public async Task LoadsOneGraphAndKeepsBaseDataWhenCalculationFails(int expenses, bool fails)
    {
        var trip = TestTrips.Create("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        for (var i = 0; i < expenses; i++)
            trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Expense", payer.Id, [debtor.Id]);
        var store = new Store(trip);
        using var cancellation = new CancellationTokenSource();
        var result = Assert.IsType<TripSnapshotResult>(await new GetTripSnapshotHandler(store, new AllowTripAccess()).HandleAsync(trip.Id, cancellation.Token));
        Assert.Equal(1, store.Calls);
        Assert.Equal(cancellation.Token, store.Token);
        Assert.Equal(trip.Id, result.Trip.Id);
        Assert.Equal(2, result.Participants.Count);
        Assert.Equal(expenses, result.Expenses.Count);
        Assert.Equal(fails, result.Settlement is null);
        Assert.Equal(fails, result.CalculationError is not null);
    }

    [Fact]
    public async Task MissingTripReturnsNull()
    {
        Assert.Null(await new GetTripSnapshotHandler(new Store(null), new AllowTripAccess()).HandleAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private sealed class Store(Trip? trip) : TripStoreStub
    {
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }
        public override Task<Trip?> FindWithParticipantsAndExpensesByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            Token = cancellationToken;
            return Task.FromResult(trip);
        }
    }
}
