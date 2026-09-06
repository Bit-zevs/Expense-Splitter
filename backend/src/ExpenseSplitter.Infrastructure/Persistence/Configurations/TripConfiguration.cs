using ExpenseSplitter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseSplitter.Infrastructure.Persistence.Configurations;

internal sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");
        builder.HasKey(trip => trip.Id);
        builder.Property(trip => trip.Id).ValueGeneratedNever();
        builder.Property(trip => trip.Name).IsRequired();
        builder.Property(trip => trip.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasPrecision(6)
            .ValueGeneratedNever();

        builder.HasMany(trip => trip.Participants)
            .WithOne()
            .HasForeignKey("TripId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(trip => trip.Expenses)
            .WithOne()
            .HasForeignKey("TripId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(trip => trip.Participants)
            .HasField("_participants")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(trip => trip.Expenses)
            .HasField("_expenses")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
