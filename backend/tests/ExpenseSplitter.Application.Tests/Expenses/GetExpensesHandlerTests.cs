using ExpenseSplitter.Application.Expenses;
using ExpenseSplitter.Application.Expenses.GetExpenses;
using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Entities;
using Xunit;

namespace ExpenseSplitter.Application.Tests.Expenses;

public sealed class GetExpensesHandlerTests
{
    [Fact]
    public async Task ReturnsTripExpensesWithShares()
    {
        var trip = new Trip("Summer vacation");
        var payer = trip.AddParticipant("Alice");
        var participant = trip.AddParticipant("Bob");
        var expense = trip.AddEqualExpense(12.34m, "Coffee", payer.Id, [participant.Id]);
        var store = new StubTripStore(trip);
        var handler = new GetExpensesHandler(store);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(trip.Id, cancellation.Token);

        var actual = Assert.Single(Assert.IsAssignableFrom<IReadOnlyCollection<ExpenseResult>>(result));
        Assert.Equal(expense.Id, actual.Id);
        Assert.Equal(expense.Amount, actual.Amount);
        Assert.Equal(expense.Description, actual.Description);
        Assert.Equal(expense.PaidByParticipantId, actual.PaidByParticipantId);
        Assert.Equal("equal", actual.SplitType);
        Assert.Equal(expense.CreatedAt, actual.CreatedAt);
        var share = Assert.Single(actual.Shares);
        Assert.Equal(participant.Id, share.ParticipantId);
        Assert.Equal(expense.Amount, share.Amount);
        Assert.Equal(trip.Id, store.RequestedId);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ReturnsEmptyCollectionWhenTripHasNoExpenses()
    {
        var store = new StubTripStore(new Trip("Summer vacation"));
        var handler = new GetExpensesHandler(store);

        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReturnsNullWhenTripDoesNotExist()
    {
        var store = new StubTripStore(null);
        var handler = new GetExpensesHandler(store);

        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubTripStore(Trip? trip) : ITripStore
    {
        public Guid RequestedId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task AddAsync(Trip tripToAdd, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Trip?> FindAggregateByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            RequestedId = id;
            CancellationToken = cancellationToken;
            return Task.FromResult(trip);
        }

        public Task<Trip?> FindAggregateForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
