using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Npgsql;

namespace ExpenseSplitter.Infrastructure.Persistence;

internal sealed class TripStore(ExpenseSplitterDbContext dbContext) : ITripStore
{
    public async Task AddAsync(Trip trip, CancellationToken cancellationToken)
    {
        await dbContext.Trips.AddAsync(trip, cancellationToken);
    }

    public Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Trips
            .AsNoTracking()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Trip?> FindTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Trips.SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Trip?> FindWithParticipantsByIdAsync(
        Guid id,
        CancellationToken cancellationToken) => dbContext.Trips
            .AsNoTracking()
            .Include(trip => trip.Participants)
            .AsSingleQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Trip?> FindWithExpensesByIdAsync(
        Guid id,
        CancellationToken cancellationToken) => dbContext.Trips
            .AsNoTracking()
            .Include(trip => trip.Expenses)
            .AsSingleQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public async Task<Trip?> FindWithParticipantsAndExpensesByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        var trip = await dbContext.Trips
            .AsNoTracking()
            .Include(trip => trip.Participants)
            .Include(trip => trip.Expenses)
            .AsSplitQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return trip;
    }

    public Task<Trip?> FindWithParticipantsTrackedAsync(
        Guid id,
        CancellationToken cancellationToken) => dbContext.Trips
            .Include(trip => trip.Participants)
            .AsSingleQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Trip?> FindWithExpensesTrackedAsync(
        Guid id,
        CancellationToken cancellationToken) => dbContext.Trips
            .Include(trip => trip.Expenses)
            .AsSingleQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Trip?> FindWithParticipantsAndExpensesTrackedAsync(
        Guid id,
        CancellationToken cancellationToken) => dbContext.Trips
            .Include(trip => trip.Participants)
            .Include(trip => trip.Expenses)
            .AsSplitQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Participant?> FindParticipantByIdAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken) => dbContext.Participants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                participant => participant.Id == participantId
                    && EF.Property<Guid>(participant, "TripId") == tripId,
                cancellationToken);

    public Task<Expense?> FindExpenseByIdAsync(
        Guid tripId,
        Guid expenseId,
        CancellationToken cancellationToken) => dbContext.Expenses
            .AsNoTracking()
            .Include(expense => expense.Shares)
            .AsSingleQuery()
            .SingleOrDefaultAsync(
                expense => expense.Id == expenseId
                    && EF.Property<Guid>(expense, "TripId") == tripId,
                cancellationToken);

    public void Remove(Trip trip)
    {
        dbContext.Trips.Remove(trip);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new WriteConflictException(exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres && IsWriteConflict(postgres))
        {
            throw new WriteConflictException(exception);
        }
        // Deferred foreign keys are checked at commit, outside EF's update wrapper.
        catch (PostgresException exception) when (IsWriteConflict(exception))
        {
            throw new WriteConflictException(exception);
        }
    }

    private static bool IsWriteConflict(PostgresException exception) => exception.SqlState is
        PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
}
