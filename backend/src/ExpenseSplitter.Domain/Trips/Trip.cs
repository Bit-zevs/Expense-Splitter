namespace ExpenseSplitter.Domain.Trips;

public sealed class Trip
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<Participant> Participants { get; init; } = Array.Empty<Participant>();

    public IReadOnlyCollection<Expense> Expenses { get; init; } = Array.Empty<Expense>();
}
