using ExpenseSplitter.Domain.ValueObjects;

namespace ExpenseSplitter.Domain.Services;

internal static class EqualSplitCalculator
{
    public static IReadOnlyCollection<ExpenseShare> Calculate(
        decimal amount,
        IReadOnlyCollection<Guid> participantIds)
    {
        ArgumentNullException.ThrowIfNull(participantIds);

        if (!MoneyLimits.IsValidPositiveAmount(amount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                $"Amount must be positive, no greater than {MoneyLimits.MaximumAmount}, "
                + "and have at most two decimal places.");
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
        var participantCount = orderedIds.Length;
        var totalCents = amount * 100m;
        var remainder = totalCents % participantCount;
        var baseCents = (totalCents - remainder) / participantCount;
        var baseAmount = baseCents / 100m;
        var shares = orderedIds.Select((id, index) =>
            new ExpenseShare(id, baseAmount + (index < remainder ? 0.01m : 0m))).ToArray();

        return Array.AsReadOnly(shares);
    }
}
