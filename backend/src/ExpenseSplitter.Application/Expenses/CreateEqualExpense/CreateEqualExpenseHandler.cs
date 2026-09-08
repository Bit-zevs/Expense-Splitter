using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.ValueObjects;

namespace ExpenseSplitter.Application.Expenses.CreateEqualExpense;

public sealed record CreateEqualExpenseCommand(
    decimal Amount,
    string? Description,
    Guid PaidByParticipantId,
    IReadOnlyCollection<Guid>? ParticipantIds,
    DateTimeOffset? OccurredAt = null);

public sealed class CreateEqualExpenseHandler(ITripStore tripStore)
{
    public async Task<ExpenseResult?> HandleAsync(
        Guid tripId,
        CreateEqualExpenseCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command);

        var trip = await tripStore.FindWithParticipantsForUpdateAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var expense = command.ParticipantIds is null
            ? trip.AddEqualExpenseForAll(
                command.Amount,
                command.Description!,
                command.PaidByParticipantId,
                command.OccurredAt)
            : trip.AddEqualExpense(
                command.Amount,
                command.Description!,
                command.PaidByParticipantId,
                command.ParticipantIds,
                command.OccurredAt);

        await tripStore.SaveChangesAsync(cancellationToken);

        return ExpenseResult.FromExpense(expense);
    }

    private static void ValidateCommand(CreateEqualExpenseCommand command)
    {
        if (!MoneyLimits.IsValidPositiveAmount(command.Amount))
        {
            throw new ArgumentOutOfRangeException(
                "amount",
                $"Amount must be positive, no greater than {MoneyLimits.MaximumAmount}, "
                + "and have at most two decimal places.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(command.Description, "description");

        if (command.PaidByParticipantId == Guid.Empty)
        {
            throw new ArgumentException("Payer ID cannot be empty.", "paidByParticipantId");
        }

        if (command.ParticipantIds is null)
        {
            return;
        }

        if (command.ParticipantIds.Count == 0)
        {
            throw new ArgumentException("Select at least one participant.", "participantIds");
        }

        if (command.ParticipantIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Participant IDs cannot be empty.", "participantIds");
        }

        if (command.ParticipantIds.Distinct().Count() != command.ParticipantIds.Count)
        {
            throw new ArgumentException("Selected participants must be unique.", "participantIds");
        }
    }
}
