using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Expenses.GetExpense;

public sealed class GetExpenseHandler(ITripStore tripStore, ICurrentAccount account)
{
    public async Task<ExpenseResult?> HandleAsync(
        Guid tripId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        var expense = await tripStore.FindExpenseByIdAsync(
            tripId, expenseId, account.Id, cancellationToken);

        return expense is null ? null : ExpenseResult.FromExpense(expense);
    }
}
