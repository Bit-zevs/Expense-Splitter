using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Expenses.GetExpenses;

public sealed class GetExpensesHandler(ITripStore tripStore)
{
    public async Task<IReadOnlyCollection<ExpenseResult>?> HandleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindWithExpensesByIdAsync(tripId, cancellationToken);

        return trip?.Expenses
            .OrderBy(expense => expense.OccurredAt)
            .ThenBy(expense => expense.Id)
            .Select(ExpenseResult.FromExpense)
            .ToArray();
    }
}
