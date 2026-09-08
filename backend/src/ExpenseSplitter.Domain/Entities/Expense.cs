using ExpenseSplitter.Domain.Enums;
using ExpenseSplitter.Domain.ValueObjects;

namespace ExpenseSplitter.Domain.Entities;

public sealed class Expense
{
    public Guid Id { get; private set; }

    public decimal Amount { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public Guid PaidByParticipantId { get; private set; }

    public IReadOnlyCollection<Guid> ParticipantIds =>
        Array.AsReadOnly(_shares.Select(share => share.ParticipantId).ToArray());

    public IReadOnlyCollection<ExpenseShare> Shares => _shares.AsReadOnly();

    public SplitType SplitType { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private readonly List<ExpenseShare> _shares = [];

    // Required by EF Core.
    private Expense() { }

    internal Expense(
        decimal amount,
        string description,
        Guid paidByParticipantId,
        SplitType splitType,
        IEnumerable<ExpenseShare> shares,
        DateTimeOffset? occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(shares);

        if (!MoneyLimits.IsValidPositiveAmount(amount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                $"Amount must be positive, no greater than {MoneyLimits.MaximumAmount}, "
                + "and have at most two decimal places.");
        }

        if (paidByParticipantId == Guid.Empty)
        {
            throw new ArgumentException("Payer ID cannot be empty.", nameof(paidByParticipantId));
        }

        if (!Enum.IsDefined(splitType))
        {
            throw new ArgumentOutOfRangeException(nameof(splitType));
        }

        var materializedShares = shares.ToArray();
        if (materializedShares.Length == 0)
        {
            throw new ArgumentException("An expense must contain at least one share.", nameof(shares));
        }

        if (materializedShares.Any(share => share is null))
        {
            throw new ArgumentException("Expense shares cannot contain null values.", nameof(shares));
        }

        if (materializedShares.Select(share => share.ParticipantId).Distinct().Count()
            != materializedShares.Length)
        {
            throw new ArgumentException("Expense shares must have unique participants.", nameof(shares));
        }

        var shareTotal = materializedShares.Aggregate(
            0m,
            (total, share) => MoneyLimits.AddExact(total, share.Amount));

        if (shareTotal != amount)
        {
            throw new ArgumentException("The sum of expense shares must equal the expense amount.", nameof(shares));
        }

        Id = Guid.NewGuid();
        Amount = amount;
        Description = description.Trim();
        PaidByParticipantId = paidByParticipantId;
        SplitType = splitType;
        var utcOccurredAt = (occurredAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        OccurredAt = new DateTimeOffset(
            utcOccurredAt.Year,
            utcOccurredAt.Month,
            utcOccurredAt.Day,
            utcOccurredAt.Hour,
            utcOccurredAt.Minute,
            0,
            TimeSpan.Zero);
        _shares.AddRange(materializedShares);
    }
}
