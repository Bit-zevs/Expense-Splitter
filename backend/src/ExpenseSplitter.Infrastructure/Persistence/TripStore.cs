using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;

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

    public Task<Trip?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
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

    public Task<Trip?> FindWithParticipantsForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken) => dbContext.Trips
            .Include(trip => trip.Participants)
            .AsSingleQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Trip?> FindWithExpensesForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken) => dbContext.Trips
            .Include(trip => trip.Expenses)
            .AsSingleQuery()
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);

    public Task<Trip?> FindWithParticipantsAndExpensesForUpdateAsync(
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
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
