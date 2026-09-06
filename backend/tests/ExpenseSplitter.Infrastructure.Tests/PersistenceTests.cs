using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.Services;
using ExpenseSplitter.Domain.ValueObjects;
using ExpenseSplitter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace ExpenseSplitter.Infrastructure.Tests;

public sealed class PersistenceTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const decimal MaximumExpense = 792281625142643375935439503.35m;

    [Fact]
    public async Task RoundTripsAggregateWithMaximumAmountZeroSharesAndDuplicateUnicodeNames()
    {
        var options = await database.CreateDatabaseAsync();
        var trip = new Trip(new string('Я', 400));
        var payer = trip.AddParticipant(new string('А', 400));
        var debtor = trip.AddParticipant(payer.Name);
        trip.AddParticipant("Третий участник");
        trip.AddEqualExpense(MaximumExpense, new string('Б', 4000), payer.Id, new[] { debtor.Id });
        trip.AddEqualExpenseForAll(0.01m, "Копейка", payer.Id);

        await using (var write = new ExpenseSplitterDbContext(options))
        {
            write.Trips.Add(trip);
            await write.SaveChangesAsync();
        }

        await using var read = new ExpenseSplitterDbContext(options);
        var loaded = await LoadTripAsync(read, trip.Id);
        Assert.NotSame(trip, loaded);
        Assert.Equal(trip.Name, loaded.Name);
        AssertTimestamp(trip.CreatedAt, loaded.CreatedAt);
        Assert.Equal(
            trip.Participants.OrderBy(participant => participant.Id).Select(participant => (participant.Id, participant.Name)),
            loaded.Participants.OrderBy(participant => participant.Id).Select(participant => (participant.Id, participant.Name)));
        Assert.Equal(trip.Expenses.Count, loaded.Expenses.Count);

        foreach (var expected in trip.Expenses)
        {
            var actual = Assert.Single(loaded.Expenses, expense => expense.Id == expected.Id);
            Assert.Equal(expected.Amount, actual.Amount);
            Assert.Equal(expected.Description, actual.Description);
            Assert.Equal(expected.PaidByParticipantId, actual.PaidByParticipantId);
            Assert.Equal(expected.SplitType, actual.SplitType);
            AssertTimestamp(expected.CreatedAt, actual.CreatedAt);
            Assert.Equal(
                expected.Shares.OrderBy(share => share.ParticipantId).Select(share => (share.ParticipantId, share.Amount)),
                actual.Shares.OrderBy(share => share.ParticipantId).Select(share => (share.ParticipantId, share.Amount)));
            Assert.Equal(actual.Amount, actual.Shares.Sum(share => share.Amount));
            Assert.Equal(actual.Shares.Select(share => share.ParticipantId), actual.ParticipantIds);
            Assert.Throws<NotSupportedException>(() => ((IList<ExpenseShare>)actual.Shares).Clear());
        }

        Assert.Throws<NotSupportedException>(() => ((IList<Participant>)loaded.Participants).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Expense>)loaded.Expenses).Clear());
    }

    [Fact]
    public async Task SavesNewParticipantAndExpenseOnReloadedAggregateWithoutRewritingExistingRows()
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, payer, _, originalExpense) = CreateTrip();
        await using (var context = new ExpenseSplitterDbContext(options))
        {
            context.Trips.Add(trip);
            await context.SaveChangesAsync();
        }

        await using (var context = new ExpenseSplitterDbContext(options))
        {
            var loaded = await LoadTripAsync(context, trip.Id);
            var participant = loaded.AddParticipant("New participant");
            loaded.AddEqualExpense(20.01m, "New expense", payer.Id, new[] { participant.Id });
            await context.SaveChangesAsync();
        }

        await using var verify = new ExpenseSplitterDbContext(options);
        var actual = await LoadTripAsync(verify, trip.Id);
        Assert.Equal(3, actual.Participants.Count);
        Assert.Equal(2, actual.Expenses.Count);
        var original = Assert.Single(actual.Expenses, expense => expense.Id == originalExpense.Id);
        Assert.Equal(originalExpense.ParticipantIds, original.ParticipantIds);
        Assert.Equal(originalExpense.Amount, original.Amount);
    }

    [Fact]
    public async Task SettlementIsStableAcrossOppositeParticipantMaterializationOrders()
    {
        var options = await database.CreateDatabaseAsync();
        var trip = new Trip("Ordering");
        var participants = Enumerable.Range(1, 4)
            .Select(index => trip.AddParticipant($"Participant {index}"))
            .ToArray();
        trip.AddEqualExpense(30m, "First", participants[0].Id, new[] { participants[1].Id });
        trip.AddEqualExpense(20m, "Second", participants[2].Id, new[] { participants[3].Id });
        var expected = SettlementCalculator.Calculate(trip);
        await using (var write = new ExpenseSplitterDbContext(options))
        {
            write.Trips.Add(trip);
            await write.SaveChangesAsync();
        }

        // Populate participants first, so relationship fixup deliberately gives opposite orders.
        await using var ascending = new ExpenseSplitterDbContext(options);
        await ascending.Participants.OrderBy(participant => participant.Id).LoadAsync();
        var first = await LoadTripAsync(ascending, trip.Id);
        await using var descending = new ExpenseSplitterDbContext(options);
        await descending.Participants.OrderByDescending(participant => participant.Id).LoadAsync();
        var second = await LoadTripAsync(descending, trip.Id);

        Assert.Equal(first.Participants.Select(participant => participant.Id).Reverse(),
            second.Participants.Select(participant => participant.Id));
        Assert.Equal(expected, SettlementCalculator.Calculate(first));
        Assert.Equal(expected, SettlementCalculator.Calculate(second));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletingTripCascadesEntireAggregate(bool loadDependents)
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, _, _, _) = CreateTrip();
        var otherTrip = new Trip("Keep this trip");
        otherTrip.AddParticipant("Keep this participant");
        await using (var write = new ExpenseSplitterDbContext(options))
        {
            write.Trips.AddRange(trip, otherTrip);
            await write.SaveChangesAsync();
        }

        await using (var delete = new ExpenseSplitterDbContext(options))
        {
            var loaded = loadDependents
                ? await LoadTripAsync(delete, trip.Id)
                : await delete.Trips.SingleAsync(candidate => candidate.Id == trip.Id);
            delete.Trips.Remove(loaded);
            await delete.SaveChangesAsync();
        }

        await using var verify = new ExpenseSplitterDbContext(options);
        Assert.Equal(otherTrip.Id, (await verify.Trips.SingleAsync()).Id);
        Assert.Single(await verify.Participants.ToListAsync());
        Assert.Empty(await verify.Expenses.ToListAsync());
        Assert.Equal(0, await CountSharesAsync(verify));
    }

    [Fact]
    public async Task DeletingExpenseInDatabaseDeletesOnlyItsShares()
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, _, _, expense) = CreateTrip();
        await using (var write = new ExpenseSplitterDbContext(options))
        {
            write.Trips.Add(trip);
            await write.SaveChangesAsync();
        }

        await using var context = new ExpenseSplitterDbContext(options);
        // ExecuteDelete bypasses change tracking: this verifies the database cascade itself.
        Assert.Equal(1, await context.Expenses.Where(candidate => candidate.Id == expense.Id).ExecuteDeleteAsync());
        Assert.Equal(0, await CountSharesAsync(context));
        Assert.Equal(2, await context.Participants.CountAsync());
        Assert.Single(await context.Trips.ToListAsync());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task CannotDeleteParticipantReferencedAsPayerOrShare(bool deletePayer, bool loadDependents)
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, payer, debtor, _) = CreateTrip();
        await using (var write = new ExpenseSplitterDbContext(options))
        {
            write.Trips.Add(trip);
            await write.SaveChangesAsync();
        }

        await using var delete = new ExpenseSplitterDbContext(options);
        if (loadDependents)
        {
            await LoadTripAsync(delete, trip.Id);
        }

        var id = deletePayer ? payer.Id : debtor.Id;
        var participant = await delete.Participants.SingleAsync(candidate => candidate.Id == id);
        // Loaded required relationships may reject deletion in EF before SQL is sent.
        var error = await Record.ExceptionAsync(async () =>
        {
            delete.Participants.Remove(participant);
            await delete.SaveChangesAsync();
        });
        if (loadDependents)
        {
            Assert.NotNull(error);
            Assert.True(error is InvalidOperationException or DbUpdateException);
        }
        else
        {
            var updateError = Assert.IsType<DbUpdateException>(error);
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,
                Assert.IsType<PostgresException>(updateError.InnerException).SqlState);
        }

        await using var verify = new ExpenseSplitterDbContext(options);
        Assert.Equal(2, await verify.Participants.CountAsync());
        Assert.Single(await verify.Expenses.ToListAsync());
        Assert.Equal(1, await CountSharesAsync(verify));
    }

    [Fact]
    public async Task CanDeleteParticipantWithoutExpensesOrShares()
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, _, _, _) = CreateTrip();
        var unused = trip.AddParticipant("Unused");
        await using var context = new ExpenseSplitterDbContext(options);
        context.Trips.Add(trip);
        await context.SaveChangesAsync();
        Assert.Equal(1, await context.Participants.Where(participant => participant.Id == unused.Id).ExecuteDeleteAsync());
        Assert.Equal(2, await context.Participants.CountAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TransactionCannotCommitDeletionOfReferencedParticipant(bool deletePayer)
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, payer, debtor, _) = CreateTrip();
        await using (var write = new ExpenseSplitterDbContext(options))
        {
            write.Trips.Add(trip);
            await write.SaveChangesAsync();
        }

        await using (var context = new ExpenseSplitterDbContext(options))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var id = deletePayer ? payer.Id : debtor.Id;
            Assert.Equal(1, await context.Participants.Where(participant => participant.Id == id).ExecuteDeleteAsync());
            var error = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }

        await using var verify = new ExpenseSplitterDbContext(options);
        Assert.Equal(2, await verify.Participants.CountAsync());
        Assert.Single(await verify.Expenses.ToListAsync());
        Assert.Equal(1, await CountSharesAsync(verify));
    }

    [Fact]
    public async Task CompositeKeyRejectsDuplicateShareButAllowsSameParticipantInAnotherExpense()
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, payer, debtor, expense) = CreateTrip();
        trip.AddEqualExpense(1m, "Another expense", payer.Id, new[] { debtor.Id });
        await using var context = new ExpenseSplitterDbContext(options);
        context.Trips.Add(trip);
        await context.SaveChangesAsync();
        Assert.Equal(2, await CountSharesAsync(context));

        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"ExpenseParticipants\" (\"ExpenseId\", \"ParticipantId\", \"Amount\") VALUES ({expense.Id}, {debtor.Id}, {10m})"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Theory]
    [InlineData("expense-trip")]
    [InlineData("participant-trip")]
    [InlineData("payer")]
    [InlineData("share-participant")]
    [InlineData("share-expense")]
    public async Task ForeignKeysRejectMissingPrincipals(string relationship)
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, _, _, _) = CreateTrip();
        await using var context = new ExpenseSplitterDbContext(options);
        context.Trips.Add(trip);
        await context.SaveChangesAsync();
        var missingId = Guid.NewGuid();
        FormattableString command = relationship switch
        {
            "expense-trip" => $"UPDATE \"Expenses\" SET \"TripId\" = {missingId}",
            "participant-trip" => $"UPDATE \"Participants\" SET \"TripId\" = {missingId}",
            "payer" => $"UPDATE \"Expenses\" SET \"PaidByParticipantId\" = {missingId}",
            "share-participant" => $"UPDATE \"ExpenseParticipants\" SET \"ParticipantId\" = {missingId}",
            "share-expense" => $"UPDATE \"ExpenseParticipants\" SET \"ExpenseId\" = {missingId}",
            _ => throw new ArgumentOutOfRangeException(nameof(relationship))
        };

        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(command));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Theory]
    [InlineData(false, "0")]
    [InlineData(false, "-0.01")]
    [InlineData(false, "792281625142643375935439503.36")]
    [InlineData(true, "-0.01")]
    [InlineData(true, "792281625142643375935439503.36")]
    public async Task DatabaseRejectsAmountsOutsideDomainRange(bool updateShare, string value)
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, _, _, _) = CreateTrip();
        await using var context = new ExpenseSplitterDbContext(options);
        context.Trips.Add(trip);
        await context.SaveChangesAsync();
        // Cast text in PostgreSQL to cover values exceeding .NET's exact cents coefficient.
        FormattableString command = updateShare
            ? (FormattableString)$"UPDATE \"ExpenseParticipants\" SET \"Amount\" = CAST({value} AS numeric)"
            : $"UPDATE \"Expenses\" SET \"Amount\" = CAST({value} AS numeric)";

        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(command));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsUnknownSplitType()
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, _, _, _) = CreateTrip();
        await using var context = new ExpenseSplitterDbContext(options);
        context.Trips.Add(trip);
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "UPDATE \"Expenses\" SET \"SplitType\" = 999"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task FailedSaveRollsBackEntireAggregate()
    {
        var options = await database.CreateDatabaseAsync();
        var (trip, _, _, expense) = CreateTrip();
        await using (var write = new ExpenseSplitterDbContext(options))
        {
            write.Trips.Add(trip);
            write.Entry(expense).Property(candidate => candidate.PaidByParticipantId).CurrentValue = Guid.NewGuid();
            var error = await Record.ExceptionAsync(() => write.SaveChangesAsync());
            // Deferred FK failures can surface from transaction commit, outside the update batch.
            var postgresError = Assert.IsType<PostgresException>(
                error is DbUpdateException updateError ? updateError.InnerException : error);
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresError.SqlState);
        }

        await using var verify = new ExpenseSplitterDbContext(options);
        Assert.Empty(await verify.Trips.ToListAsync());
        Assert.Empty(await verify.Participants.ToListAsync());
        Assert.Empty(await verify.Expenses.ToListAsync());
        Assert.Equal(0, await CountSharesAsync(verify));
    }

    [Fact]
    public async Task InitialMigrationCanBeRolledBackAndReapplied()
    {
        var options = await database.CreateDatabaseAsync();
        await using var context = new ExpenseSplitterDbContext(options);
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());
        await context.Database.MigrateAsync();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        var (trip, _, _, _) = CreateTrip();
        context.Trips.Add(trip);
        await context.SaveChangesAsync();
        Assert.Equal(1, await CountSharesAsync(context));
    }

    private static (Trip Trip, Participant Payer, Participant Debtor, Expense Expense) CreateTrip()
    {
        var trip = new Trip("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        var expense = trip.AddEqualExpense(10m, "Expense", payer.Id, new[] { debtor.Id });
        return (trip, payer, debtor, expense);
    }

    private static Task<Trip> LoadTripAsync(ExpenseSplitterDbContext context, Guid id) => context.Trips
        .Include(trip => trip.Participants)
        .Include(trip => trip.Expenses)
        .AsSplitQuery()
        .SingleAsync(trip => trip.Id == id);

    private static Task<int> CountSharesAsync(ExpenseSplitterDbContext context) => context.Database
        .SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM \"ExpenseParticipants\"")
        .SingleAsync();

    private static void AssertTimestamp(DateTimeOffset expected, DateTimeOffset actual)
    {
        // PostgreSQL timestamps have microsecond precision; .NET ticks are 100 ns.
        Assert.Equal(TimeSpan.Zero, actual.Offset);
        Assert.InRange(expected.Ticks - actual.Ticks, 0L, 9L);
    }
}
