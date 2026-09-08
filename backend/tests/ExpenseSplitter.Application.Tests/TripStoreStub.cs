using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Application.Tests;

internal abstract class TripStoreStub : ITripStore
{
    public virtual Task AddAsync(Trip trip, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<Trip?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public virtual Task<Trip?> FindWithParticipantsByIdAsync(
        Guid id,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<Trip?> FindWithExpensesByIdAsync(
        Guid id,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<Trip?> FindWithParticipantsAndExpensesByIdAsync(
        Guid id,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<Trip?> FindWithParticipantsForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<Trip?> FindWithExpensesForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<Trip?> FindWithParticipantsAndExpensesForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<Participant?> FindParticipantByIdAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual Task<Expense?> FindExpenseByIdAsync(
        Guid tripId,
        Guid expenseId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public virtual void Remove(Trip trip) => throw new NotSupportedException();

    public virtual Task SaveChangesAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
