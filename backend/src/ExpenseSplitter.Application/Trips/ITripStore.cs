using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Application.Trips;

public interface ITripStore
{
    Task AddAsync(Trip trip, CancellationToken cancellationToken);

    Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindWithParticipantsByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindWithExpensesByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Trip?> FindWithParticipantsAndExpensesByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Trip?> FindWithParticipantsForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Participant?> FindParticipantByIdAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken);

    Task<Expense?> FindExpenseByIdAsync(
        Guid tripId,
        Guid expenseId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
