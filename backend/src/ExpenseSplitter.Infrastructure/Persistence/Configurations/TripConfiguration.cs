using ExpenseSplitter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ExpenseSplitter.Infrastructure.Identity;

namespace ExpenseSplitter.Infrastructure.Persistence.Configurations;

internal sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");
        builder.HasKey(trip => trip.Id);
        builder.Property(trip => trip.Id).ValueGeneratedNever();
        builder.Property(trip => trip.Name).IsRequired();
        builder.Property<int>("Revision").HasDefaultValue(0);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.OwnerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(t => t.JoinCodeHash).HasColumnType("bytea");
        builder.HasIndex(t => t.JoinCodeHash).IsUnique().HasFilter("\"JoinCodeHash\" IS NOT NULL");
        builder.ToTable(table => table.HasCheckConstraint("CK_Trips_JoinCodeHash", "\"JoinCodeHash\" IS NULL OR octet_length(\"JoinCodeHash\") = 32"));
        builder.Property(trip => trip.Currency)
            .HasMaxLength(3)
            .HasDefaultValue(Trip.DefaultCurrency)
            .IsRequired();
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Trips_Currency",
            "\"Currency\" IN ('RUB', 'EUR', 'USD')"));
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
