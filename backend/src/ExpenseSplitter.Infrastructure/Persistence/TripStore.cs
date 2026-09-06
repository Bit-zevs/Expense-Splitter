using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseSplitter.Infrastructure.Persistence;

internal sealed class TripStore(ExpenseSplitterDbContext dbContext) : ITripStore
{
    public async Task AddAsync(Trip trip, CancellationToken cancellationToken)
    {
        await dbContext.Trips.AddAsync(trip, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Trips
            .AsNoTracking()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Trip?> FindWithParticipantsByIdAsync(
        Guid id,
        CancellationToken cancellationToken) => dbContext.Trips
            .AsNoTracking()
            .Include(trip => trip.Participants)
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Trip?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Trips
            .Include(trip => trip.Participants)
            .Include(trip => trip.Expenses)
            .AsSplitQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
