namespace ExpenseSplitter.Domain.Entities;

public sealed class TripJoinRequest
{
    private TripJoinRequest() { }

    public TripJoinRequest(Guid tripId, Guid accountId)
    {
        if (tripId == Guid.Empty || accountId == Guid.Empty)
            throw new ArgumentException("Trip and account are required.");
        Id = Guid.NewGuid();
        TripId = tripId;
        AccountId = accountId;
        RequestedAt = DateTimeOffset.UtcNow;
        RequestedAt = RequestedAt.AddTicks(-(RequestedAt.Ticks % 10));
    }

    public Guid Id { get; private set; }
    public Guid TripId { get; private set; }
    public Guid AccountId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
}
