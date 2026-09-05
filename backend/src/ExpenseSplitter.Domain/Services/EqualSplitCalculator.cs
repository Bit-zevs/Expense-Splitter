using ExpenseSplitter.Domain.ValueObjects;

namespace ExpenseSplitter.Domain.Services;

internal static class EqualSplitCalculator
{
    public static IReadOnlyCollection<ExpenseShare> Calculate(
        decimal amount,
        IReadOnlyCollection<Guid> participantIds)
    {
        ArgumentNullException.ThrowIfNull(participantIds);

        if (amount <= 0 || amount > decimal.MaxValue / 100m || amount % 0.01m != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount must be positive, fit in decimal cents, and have at most two decimal places.");
        }

        if (participantIds.Count == 0)
        {
            throw new ArgumentException("Select at least one participant.", nameof(participantIds));
        }

        if (participantIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Participant IDs cannot be empty.", nameof(participantIds));
        }

        if (participantIds.Distinct().Count() != participantIds.Count)
        {
            throw new ArgumentException("Selected participants must be unique.", nameof(participantIds));
        }

        var orderedIds = participantIds.Order().ToArray();
        var cents = amount * 100m;
        var remainder = cents % orderedIds.Length;
        var baseCents = (cents - remainder) / orderedIds.Length;
        var shares = orderedIds.Select((id, index) =>
            new ExpenseShare(id, (baseCents + (index < remainder ? 1m : 0m)) / 100m)).ToArray();

        return Array.AsReadOnly(shares);
    }
}
