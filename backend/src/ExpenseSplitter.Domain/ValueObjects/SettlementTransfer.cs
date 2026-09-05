namespace ExpenseSplitter.Domain.ValueObjects;

public sealed record SettlementTransfer
{
    internal SettlementTransfer(
        Guid fromParticipantId,
        Guid toParticipantId,
        decimal amount)
    {
        if (fromParticipantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Sender participant ID cannot be empty.",
                nameof(fromParticipantId));
        }

        if (toParticipantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Recipient participant ID cannot be empty.",
                nameof(toParticipantId));
        }

        if (fromParticipantId == toParticipantId)
        {
            throw new ArgumentException(
                "Sender and recipient must be different participants.",
                nameof(toParticipantId));
        }

        if (amount <= 0 || amount > decimal.MaxValue / 100m || amount % 0.01m != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Transfer amount must be positive, fit in decimal cents, and have at most two decimal places.");
        }

        FromParticipantId = fromParticipantId;
        ToParticipantId = toParticipantId;
        Amount = amount;
    }

    public Guid FromParticipantId { get; }

    public Guid ToParticipantId { get; }

    public decimal Amount { get; }
}
