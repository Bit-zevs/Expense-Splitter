using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Application.Trips;

public interface ITripStore
{
    Task AddAsync(Trip trip, CancellationToken cancellationToken);

    Task<Trip?> FindByIdAsync(Guid id, Guid accountId, CancellationToken cancellationToken);

    Task<Trip?> FindTrackedAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindWithParticipantsByIdAsync(Guid id, Guid accountId, CancellationToken cancellationToken);

    Task<Trip?> FindWithExpensesByIdAsync(Guid id, Guid accountId, CancellationToken cancellationToken);

    Task<Trip?> FindWithParticipantsAndExpensesByIdAsync(
        Guid id,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<Trip?> FindWithParticipantsTrackedAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Trip?> FindWithExpensesTrackedAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Trip?> FindWithParticipantsAndExpensesTrackedAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Expense?> FindExpenseByIdAsync(
        Guid tripId,
        Guid expenseId,
        Guid accountId,
        CancellationToken cancellationToken);

    void Remove(Trip trip);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
