using ExpenseSplitter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseSplitter.Infrastructure.Persistence;

public sealed class ExpenseSplitterDbContext(DbContextOptions<ExpenseSplitterDbContext> options)
    : DbContext(options)
{
    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<Participant> Participants => Set<Participant>();

    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExpenseSplitterDbContext).Assembly);
    }
}
