namespace ExpenseSplitter.Domain.Entities;

public sealed class Participant
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    // Required by EF Core.
    private Participant() { }

    internal Participant(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = Guid.NewGuid();
        Name = name.Trim();
    }
}
