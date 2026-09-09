using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Expenses.GetExpenses;

public sealed class GetExpensesHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<IReadOnlyCollection<ExpenseResult>?> HandleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        await access.RequireAsync(tripId, ownerOnly: false, write: false, cancellationToken);
        var trip = await tripStore.FindWithExpensesByIdAsync(tripId, cancellationToken);

        return trip?.Expenses
            .OrderBy(expense => expense.OccurredAt)
            .ThenBy(expense => expense.Id)
            .Select(ExpenseResult.FromExpense)
            .ToArray();
    }
}
