using ExpenseSplitter.Application.Expenses.GetExpense;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Expenses;

public sealed class GetExpenseHandlerTests
{
    [Fact]
    public async Task ReturnsExpenseFromRequestedTrip()
    {
        var trip = new Trip("Trip");
        var participant = trip.AddParticipant("Alice");
        var expense = trip.AddEqualExpenseForAll(12.34m, "Coffee", participant.Id);
        var requestedTripId = Guid.NewGuid();
        var store = new StubTripStore(expense);
        var handler = new GetExpenseHandler(store);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            requestedTripId,
            expense.Id,
            cancellation.Token);

        Assert.NotNull(result);
        Assert.Equal(expense.Id, result.Id);
        Assert.Equal(expense.Amount, result.Amount);
        Assert.Equal(expense.Description, result.Description);
        Assert.Equal(requestedTripId, store.RequestedTripId);
        Assert.Equal(expense.Id, store.RequestedExpenseId);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ReturnsNullWhenExpenseDoesNotExist()
    {
        var handler = new GetExpenseHandler(new StubTripStore(null));

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubTripStore(Expense? expense) : TripStoreStub
    {
        public Guid RequestedTripId { get; private set; }

        public Guid RequestedExpenseId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public override Task<Expense?> FindExpenseByIdAsync(
            Guid tripId,
            Guid expenseId,
            CancellationToken cancellationToken)
        {
            RequestedTripId = tripId;
            RequestedExpenseId = expenseId;
            CancellationToken = cancellationToken;
            return Task.FromResult(expense);
        }
    }
}
