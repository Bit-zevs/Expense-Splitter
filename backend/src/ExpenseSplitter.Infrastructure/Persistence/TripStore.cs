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

    public Task<Trip?> FindByIdAsync(Guid id, Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Trips.AsNoTracking().SingleOrDefaultAsync(
            trip => trip.Id == id && (trip.OwnerAccountId == accountId
                || trip.Participants.Any(p => p.AccountId == accountId)),
            cancellationToken);

    public async Task<Trip?> FindTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        await BeginWriteAsync(cancellationToken);
        return await dbContext.Trips.SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);
    }

    public Task<Trip?> FindWithParticipantsByIdAsync(
        Guid id, Guid accountId, CancellationToken cancellationToken) => dbContext.Trips
        .AsNoTracking()
        .Include(trip => trip.Participants)
        .AsSingleQuery()
        .SingleOrDefaultAsync(
            trip => trip.Id == id && (trip.OwnerAccountId == accountId
                || trip.Participants.Any(p => p.AccountId == accountId)),
            cancellationToken);

    public Task<Trip?> FindWithExpensesByIdAsync(
        Guid id, Guid accountId, CancellationToken cancellationToken) => dbContext.Trips
        .AsNoTracking()
        .Include(trip => trip.Expenses)
        .AsSingleQuery()
        .SingleOrDefaultAsync(
            trip => trip.Id == id && (trip.OwnerAccountId == accountId
                || trip.Participants.Any(p => p.AccountId == accountId)),
            cancellationToken);

    public async Task<Trip?> FindWithParticipantsAndExpensesByIdAsync(
        Guid id, Guid accountId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var trip = await dbContext.Trips
            .AsNoTracking()
            .Include(t => t.Participants)
            .Include(t => t.Expenses)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                t => t.Id == id && (t.OwnerAccountId == accountId
                    || t.Participants.Any(p => p.AccountId == accountId)),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return trip;
    }

    public async Task<Trip?> FindWithParticipantsTrackedAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await BeginWriteAsync(cancellationToken);
        return await dbContext.Trips
            .Include(trip => trip.Participants)
            .AsSingleQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);
    }

    public async Task<Trip?> FindWithExpensesTrackedAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await BeginWriteAsync(cancellationToken);
        return await dbContext.Trips
            .Include(trip => trip.Expenses)
            .AsSingleQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);
    }

    public async Task<Trip?> FindWithParticipantsAndExpensesTrackedAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await BeginWriteAsync(cancellationToken);
        return await dbContext.Trips
            .Include(trip => trip.Participants)
            .Include(trip => trip.Expenses)
            .AsSplitQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);
    }

    private async Task BeginWriteAsync(CancellationToken cancellationToken)
    {
        // The scoped DbContext rolls back if validation/not-found returns without saving.
        // Keep one snapshot through load, aggregate mutation, save and commit.
        if (dbContext.Database.CurrentTransaction is null)
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
    }

    public Task<Expense?> FindExpenseByIdAsync(
        Guid tripId, Guid expenseId, Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Expenses.AsNoTracking()
            .Include(expense => expense.Shares)
            .AsSingleQuery()
            .SingleOrDefaultAsync(
                expense => expense.Id == expenseId
                    && EF.Property<Guid>(expense, "TripId") == tripId
                    && dbContext.Trips.Any(t => t.Id == tripId && (t.OwnerAccountId == accountId
                        || t.Participants.Any(p => p.AccountId == accountId))),
                cancellationToken);

    public void Remove(Trip trip)
    {
        dbContext.Trips.Remove(trip);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsWriteConflict(exception))
        {
            throw new WriteConflictException(exception);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    // Commit-time FK failures are unwrapped; Npgsql may wrap serialization failures
    // in InvalidOperationException -> DbUpdateException -> PostgresException.
    internal static bool IsWriteConflict(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        PostgresException postgres => postgres.SqlState is
            PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected,
        DbUpdateException or InvalidOperationException when exception.InnerException is not null =>
            IsWriteConflict(exception.InnerException),
        _ => false
    };
}
