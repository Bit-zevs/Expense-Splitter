namespace ExpenseSplitter.Domain.Trips;

public sealed class Expense
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public decimal Amount { get; init; }

    public string Description { get; init; } = string.Empty;

    public Guid PaidByParticipantId { get; init; }

    public IReadOnlyCollection<Guid> ParticipantIds { get; init; } = Array.Empty<Guid>();

    public SplitType SplitType { get; init; } = SplitType.Equal;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
