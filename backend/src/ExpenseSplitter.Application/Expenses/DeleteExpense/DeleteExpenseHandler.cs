using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Expenses.DeleteExpense;

public sealed class DeleteExpenseHandler(ITripStore tripStore)
{
    public async Task<bool> HandleAsync(
        Guid tripId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindWithExpensesTrackedAsync(tripId, cancellationToken);
        if (trip is null || !trip.RemoveExpense(expenseId))
        {
            return false;
        }

        await tripStore.SaveChangesAsync(cancellationToken);
        return true;
    }
}
