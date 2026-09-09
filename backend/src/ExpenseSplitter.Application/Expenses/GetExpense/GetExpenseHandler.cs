using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Expenses.GetExpense;

public sealed class GetExpenseHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<ExpenseResult?> HandleAsync(
        Guid tripId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        await access.RequireAsync(tripId, ownerOnly: false, write: false, cancellationToken);
        var expense = await tripStore.FindExpenseByIdAsync(
            tripId,
            expenseId,
            cancellationToken);

        return expense is null ? null : ExpenseResult.FromExpense(expense);
    }
}
