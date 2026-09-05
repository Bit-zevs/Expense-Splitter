namespace ExpenseSplitter.Domain.ValueObjects;

public sealed class ExpenseShare
{
    // Required by EF Core.
    private ExpenseShare() { }

    internal ExpenseShare(Guid participantId, decimal amount)
    {
        if (participantId == Guid.Empty)
        {
            throw new ArgumentException("Participant ID cannot be empty.", nameof(participantId));
        }

        if (amount < 0 || amount > decimal.MaxValue / 100m || amount % 0.01m != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Share amount cannot be negative, must fit in decimal cents, and have at most two decimal places.");
        }

        ParticipantId = participantId;
        Amount = amount;
    }

    public Guid ParticipantId { get; private set; }

    public decimal Amount { get; private set; }
}
