using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Infrastructure.Identity;
using ExpenseSplitter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace ExpenseSplitter.Infrastructure.Tests;

public sealed class AccessConcurrencyTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task AccountsMigrationRefusesToEraseOrAssignAnonymousTrips()
    {
        var options = await database.CreateDatabaseAsync();
        await using var db = new ExpenseSplitterDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20260908124458_RenameExpenseCreatedAtToOccurredAt");
        var tripId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"Trips\" (\"Id\", \"Name\", \"Currency\", \"CreatedAt\") VALUES ({tripId}, 'Legacy', 'RUB', now())");
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
        Assert.Contains("requires an empty Trips table", error.MessageText);
        Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM \"Trips\"").SingleAsync());
    }

    [Fact]
    public async Task RevocationCannotBeBypassedByAnOlderWriteSnapshot()
    {
        var options = await database.CreateDatabaseAsync();
        var memberId = Guid.NewGuid();
        var trip = TestTrips.Create("Private");
        trip.AddAccountParticipant(TestTrips.OwnerId, "Owner");
        var member = trip.AddAccountParticipant(memberId, "Member");
        await using (var seed = new ExpenseSplitterDbContext(options))
        {
            seed.Users.Add(new ApplicationUser { Id = memberId, DisplayName = "Member" });
            seed.Trips.Add(trip);
            await seed.SaveChangesAsync();
        }
        await using var stale = new ExpenseSplitterDbContext(options);
        var staleAccess = new TripAccess(stale, new Account(memberId));
        await staleAccess.RequireAsync(trip.Id, false, false, CancellationToken.None);

        await using (var revoker = new ExpenseSplitterDbContext(options))
        {
            await new TripAccess(revoker, new TestCurrentAccount()).RequireAsync(trip.Id, true, true, CancellationToken.None);
            var tracked = await revoker.Trips.Include(t => t.Participants).SingleAsync(t => t.Id == trip.Id);
            Assert.True(tracked.RemoveParticipant(member.Id));
            await new TripStore(revoker).SaveChangesAsync(CancellationToken.None);
        }

        await Assert.ThrowsAsync<WriteConflictException>(() => staleAccess.RequireAsync(trip.Id, false, true, CancellationToken.None));
        await using var fresh = new ExpenseSplitterDbContext(options);
        var denied = await Assert.ThrowsAsync<AccessException>(() => new TripAccess(fresh, new Account(memberId))
            .RequireAsync(trip.Id, false, true, CancellationToken.None));
        Assert.Equal(404, denied.StatusCode);
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateAccountMembershipAndPendingRequests()
    {
        var options = await database.CreateDatabaseAsync();
        var trip = TestTrips.Create("Trip");
        var owner = trip.AddAccountParticipant(TestTrips.OwnerId, "Owner");
        var phantom = trip.AddParticipant("Phantom");
        await using (var db = new ExpenseSplitterDbContext(options))
        {
            db.Trips.Add(trip);
            await db.SaveChangesAsync();
            db.Entry(phantom).Property(p => p.AccountId).CurrentValue = owner.AccountId;
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var requests = new ExpenseSplitterDbContext(options);
        requests.TripJoinRequests.Add(new TripJoinRequest(trip.Id, TestTrips.OwnerId));
        requests.TripJoinRequests.Add(new TripJoinRequest(trip.Id, TestTrips.OwnerId));
        await Assert.ThrowsAsync<DbUpdateException>(() => requests.SaveChangesAsync());
    }

    private sealed class Account(Guid id) : ICurrentAccount
    {
        public Guid Id => id;
        public string DisplayName => "Member";
    }
}
