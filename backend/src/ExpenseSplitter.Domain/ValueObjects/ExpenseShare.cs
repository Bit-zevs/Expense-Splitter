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

        if (!MoneyLimits.IsValidNonNegativeAmount(amount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                $"Share amount must be between zero and {MoneyLimits.MaximumAmount} "
                + "and have at most two decimal places.");
        }

        ParticipantId = participantId;
        Amount = amount;
    }

    public Guid ParticipantId { get; private set; }

    public decimal Amount { get; private set; }
}
