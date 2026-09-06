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
}
