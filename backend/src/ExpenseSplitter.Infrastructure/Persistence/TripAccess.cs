using System.Data;
using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;
using Microsoft.EntityFrameworkCore;

namespace ExpenseSplitter.Infrastructure.Persistence;

internal sealed class TripAccess(ExpenseSplitterDbContext db, ICurrentAccount account) : ITripAccess
{
    public async Task RequireWriteAsync(Guid tripId, bool ownerOnly, CancellationToken cancellationToken)
    {
        var accountId = account.Id;
        await BeginAsync(cancellationToken);
        var ownerId = await db.Trips.Where(t => t.Id == tripId &&
                t.Participants.Any(p => p.AccountId == accountId))
            .Select(t => (Guid?)t.OwnerAccountId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId is null) throw new AccessException(404);
        if (ownerOnly && ownerId != accountId) throw new AccessException(403);
        // A changed trip revision invalidates this permission snapshot before any mutation.
        await LockAsync(tripId, cancellationToken);
    }

    internal async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
    }

    internal async Task LockAsync(Guid tripId, CancellationToken cancellationToken)
    {
        await BeginAsync(cancellationToken);
        try
        {
            // Updating the row makes a waiter with an older RR snapshot fail explicitly.
            // All membership and expense writes serialize through the same trip row.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Trips\" SET \"Revision\" = \"Revision\" + 1 WHERE \"Id\" = {tripId}", cancellationToken);
        }
        catch (Exception exception) when (TripStore.IsWriteConflict(exception))
        {
            throw new WriteConflictException(exception);
        }
    }
}
