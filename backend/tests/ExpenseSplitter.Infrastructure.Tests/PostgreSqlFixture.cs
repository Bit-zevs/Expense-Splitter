using ExpenseSplitter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ExpenseSplitter.Infrastructure.Tests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17.10")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public async Task InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await _container.StartAsync(timeout.Token);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public async Task<DbContextOptions<ExpenseSplitterDbContext>> CreateDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        // Each test owns a database inside this disposable container, never a user's database.
        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"expense_splitter_{Guid.NewGuid():N}",
            Pooling = false
        };
        var options = new DbContextOptionsBuilder<ExpenseSplitterDbContext>()
            .UseNpgsql(connectionString.ConnectionString)
            .Options;

        await using var context = new ExpenseSplitterDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);
        await context.Users.AddAsync(new Infrastructure.Identity.ApplicationUser
        { Id = TestTrips.OwnerId, DisplayName = "Test owner" }, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return options;
    }
}
