namespace ExpenseSplitter.Domain.Trips;

public sealed class Expense
{
    private Expense() { }

    public Guid Id { get; private init; } = Guid.NewGuid();

    public decimal Amount { get; private init; }

    public string Description { get; private init; } = string.Empty;

    public Guid PaidByParticipantId { get; private init; }

    public IReadOnlyCollection<Guid> ParticipantIds =>
        Array.AsReadOnly(Shares.Select(share => share.ParticipantId).ToArray());

    public IReadOnlyList<ExpenseShare> Shares { get; private init; } = Array.Empty<ExpenseShare>();

    public SplitType SplitType { get; private init; } = SplitType.Equal;

    public DateTimeOffset CreatedAt { get; private init; } = DateTimeOffset.UtcNow;

    public static Expense CreateEqual(
        Trip trip,
        decimal amount,
        string description,
        Guid paidByParticipantId,
        IEnumerable<Guid> participantIds)
    {
        ArgumentNullException.ThrowIfNull(trip);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(participantIds);

        var members = trip.Participants.Select(participant => participant.Id).ToHashSet();
        if (!members.Contains(paidByParticipantId))
            throw new ArgumentException("The payer must belong to the trip.", nameof(paidByParticipantId));

        var selected = participantIds.ToArray();
        if (selected.Any(id => !members.Contains(id)))
            throw new ArgumentException("All selected participants must belong to the trip.", nameof(participantIds));

        return new Expense
        {
            Amount = amount,
            Description = description,
            PaidByParticipantId = paidByParticipantId,
            Shares = EqualSplit.Calculate(amount, selected)
        };
    }
}
