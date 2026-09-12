using ExpenseSplitter.Application.Expenses.DeleteExpense;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Expenses;

public sealed class DeleteExpenseHandlerTests
{
    [Fact]
    public async Task RemovesExpenseThroughAggregateAndSaves()
    {
        var trip = TestTrips.Create("Trip");
        var participant = trip.AddParticipant("Alice");
        var expense = trip.AddEqualExpenseForAll(10m, "Coffee", participant.Id);
        var store = new StubTripStore(trip);

        var deleted = await new DeleteExpenseHandler(store, new AllowTripAccess())
            .HandleAsync(trip.Id, expense.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(trip.Expenses);
        Assert.Equal(1, store.SaveCount);
    }

    private sealed class StubTripStore(Trip trip) : TripStoreStub
    {
        public int SaveCount { get; private set; }

        public override Task<Trip?> FindWithExpensesTrackedAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult<Trip?>(trip);

        public override Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
