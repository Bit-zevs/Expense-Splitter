using ExpenseSplitter.Application.Trips;

namespace ExpenseSplitter.Application.Expenses.CreateEqualExpense;

public sealed record CreateEqualExpenseCommand(
    decimal Amount,
    string? Description,
    Guid PaidByParticipantId,
    IReadOnlyCollection<Guid>? ParticipantIds);

public sealed class CreateEqualExpenseHandler(ITripStore tripStore)
{
    public async Task<ExpenseResult?> HandleAsync(
        Guid tripId,
        CreateEqualExpenseCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var trip = await tripStore.FindWithParticipantsForUpdateAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var expense = command.ParticipantIds is null
            ? trip.AddEqualExpenseForAll(
                command.Amount,
                command.Description!,
                command.PaidByParticipantId)
            : trip.AddEqualExpense(
                command.Amount,
                command.Description!,
                command.PaidByParticipantId,
                command.ParticipantIds);

        await tripStore.SaveChangesAsync(cancellationToken);

        return ExpenseResult.FromExpense(expense);
    }
}
