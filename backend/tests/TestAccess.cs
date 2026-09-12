using ExpenseSplitter.Application.Access;

namespace ExpenseSplitter.Testing;

internal sealed class TestCurrentAccount : ICurrentAccount
{
    public Guid Id => TestTrips.OwnerId;
    public string DisplayName => "Owner";
}

// Existing calculation/serialization tests isolate authorization; security tests use the real implementation.
internal sealed class AllowTripAccess : ITripAccess
{
    public Task RequireWriteAsync(Guid tripId, bool ownerOnly, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
