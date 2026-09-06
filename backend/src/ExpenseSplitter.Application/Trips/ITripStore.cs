using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Application.Trips;

public interface ITripStore
{
    Task AddAsync(Trip trip, CancellationToken cancellationToken);

    Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindWithParticipantsByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindWithExpensesByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
