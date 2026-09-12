using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Expenses.DeleteExpense;

public sealed class DeleteExpenseHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<bool> HandleAsync(
        Guid tripId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        await access.RequireWriteAsync(tripId, ownerOnly: false, cancellationToken);
        var trip = await tripStore.FindWithExpensesTrackedAsync(tripId, cancellationToken);
        if (trip is null || !trip.RemoveExpense(expenseId))
        {
            return false;
        }

        await tripStore.SaveChangesAsync(cancellationToken);
        return true;
    }
}
