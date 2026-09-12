using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Expenses.GetExpenses;

public sealed class GetExpensesHandler(ITripStore tripStore, ICurrentAccount account)
{
    public async Task<IReadOnlyCollection<ExpenseResult>?> HandleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindWithExpensesByIdAsync(tripId, account.Id, cancellationToken);

        return trip?.Expenses
            .OrderBy(expense => expense.OccurredAt)
            .ThenBy(expense => expense.Id)
            .Select(ExpenseResult.FromExpense)
            .ToArray();
    }
}
