using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Expenses.GetExpenses;

public sealed class GetExpensesHandler(ITripStore tripStore)
{
    public async Task<IReadOnlyCollection<ExpenseResult>?> HandleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindAggregateByIdAsync(tripId, cancellationToken);

        return trip?.Expenses
            .OrderBy(expense => expense.CreatedAt)
            .ThenBy(expense => expense.Id)
            .Select(ExpenseResult.FromExpense)
            .ToArray();
    }
}
