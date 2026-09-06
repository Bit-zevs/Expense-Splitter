using ExpenseSplitter.Domain.Enums;
using ExpenseSplitter.Domain.ValueObjects;
using System.Numerics;

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

    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<ExpenseShare> _shares = [];

    // Required by EF Core.
    private Expense() { }

    internal Expense(
        decimal amount,
        string description,
        Guid paidByParticipantId,
        SplitType splitType,
        IEnumerable<ExpenseShare> shares)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(shares);

        if (amount <= 0 || amount > decimal.MaxValue / 100m || amount % 0.01m != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount must be positive, fit in decimal cents, and have at most two decimal places.");
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

        var shareTotalInCents = materializedShares.Aggregate(
            BigInteger.Zero,
            (total, share) => total + new BigInteger(share.Amount * 100m));
        var amountInCents = new BigInteger(amount * 100m);

        if (shareTotalInCents != amountInCents)
        {
            throw new ArgumentException("The sum of expense shares must equal the expense amount.", nameof(shares));
        }

        Id = Guid.NewGuid();
        Amount = amount;
        Description = description.Trim();
        PaidByParticipantId = paidByParticipantId;
        SplitType = splitType;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedAt = CreatedAt.AddTicks(-(CreatedAt.Ticks % 10));
        _shares.AddRange(materializedShares);
    }
}
