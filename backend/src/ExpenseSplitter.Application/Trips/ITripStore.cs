using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Application.Trips;

public interface ITripStore
{
    Task AddAsync(Trip trip, CancellationToken cancellationToken);

    Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindAggregateByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindAggregateForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
