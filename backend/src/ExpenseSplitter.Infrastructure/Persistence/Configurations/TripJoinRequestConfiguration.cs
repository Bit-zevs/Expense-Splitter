using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseSplitter.Infrastructure.Persistence.Configurations;

internal sealed class TripJoinRequestConfiguration : IEntityTypeConfiguration<TripJoinRequest>
{
    public void Configure(EntityTypeBuilder<TripJoinRequest> builder)
    {
        builder.ToTable("TripJoinRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.RequestedAt).HasPrecision(6);
        builder.HasIndex(r => new { r.TripId, r.AccountId }).IsUnique();
        builder.HasOne<Trip>().WithMany().HasForeignKey(r => r.TripId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
