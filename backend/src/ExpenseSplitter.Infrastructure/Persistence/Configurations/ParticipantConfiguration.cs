using ExpenseSplitter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ExpenseSplitter.Infrastructure.Identity;

namespace ExpenseSplitter.Infrastructure.Persistence.Configurations;

internal sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");
        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.Id).ValueGeneratedNever();
        builder.Property(participant => participant.Name).IsRequired();
        builder.Property<Guid>("TripId").IsRequired();
        builder.HasIndex("TripId");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex("TripId", nameof(Participant.AccountId)).IsUnique()
            .HasFilter("\"AccountId\" IS NOT NULL");
    }
}
