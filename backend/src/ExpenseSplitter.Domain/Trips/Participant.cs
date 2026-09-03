namespace ExpenseSplitter.Domain.Trips;

public sealed class Participant
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;
}
