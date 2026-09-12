using ExpenseSplitter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ExpenseSplitter.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ExpenseSplitter.Infrastructure.Persistence;

public sealed class ExpenseSplitterDbContext(DbContextOptions<ExpenseSplitterDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<Participant> Participants => Set<Participant>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<TripJoinRequest> TripJoinRequests => Set<TripJoinRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApplicationUser>().Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<ApplicationUser>().HasIndex(u => u.NormalizedEmail).IsUnique();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExpenseSplitterDbContext).Assembly);
    }
}
