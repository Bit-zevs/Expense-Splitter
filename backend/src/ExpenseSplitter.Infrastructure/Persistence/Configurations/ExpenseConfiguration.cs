using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseSplitter.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses", table =>
        {
            table.HasCheckConstraint("CK_Expenses_Amount",
                "\"Amount\" > 0 AND \"Amount\" <= 792281625142643375935439503.35");
            table.HasCheckConstraint("CK_Expenses_SplitType", "\"SplitType\" IN (0)");
        });

        builder.HasKey(expense => expense.Id);
        builder.Property(expense => expense.Id).ValueGeneratedNever();
        builder.Property<Guid>("TripId").IsRequired();
        builder.Property(expense => expense.Amount).HasPrecision(29, 2);
        builder.Property(expense => expense.Description).IsRequired();
        builder.Property(expense => expense.SplitType).HasConversion<int>();
        builder.Property(expense => expense.OccurredAt)
            .HasColumnType("timestamp with time zone")
            .HasPrecision(6)
            .ValueGeneratedNever();
        builder.Ignore(expense => expense.ParticipantIds);

        builder.HasOne<Participant>()
            .WithMany()
            .HasForeignKey(expense => expense.PaidByParticipantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex("TripId", nameof(Expense.OccurredAt));
        builder.HasIndex(expense => expense.PaidByParticipantId);

        builder.OwnsMany(expense => expense.Shares, shares =>
        {
            shares.ToTable("ExpenseParticipants", table =>
                table.HasCheckConstraint("CK_ExpenseParticipants_Amount",
                    "\"Amount\" >= 0 AND \"Amount\" <= 792281625142643375935439503.35"));

            var ownership = shares.WithOwner().HasForeignKey("ExpenseId");
            ownership.Metadata.DeleteBehavior = DeleteBehavior.Cascade;
            shares.Property<Guid>("ExpenseId").ValueGeneratedNever();
            shares.Property(share => share.ParticipantId).ValueGeneratedNever();
            shares.HasKey("ExpenseId", nameof(ExpenseShare.ParticipantId));
            shares.Property(share => share.Amount).HasPrecision(29, 2);

            shares.HasOne<Participant>()
                .WithMany()
                .HasForeignKey(share => share.ParticipantId)
                .IsRequired()
                .OnDelete(DeleteBehavior.NoAction);
            shares.HasIndex(share => share.ParticipantId);
        });

        builder.Navigation(expense => expense.Shares)
            .HasField("_shares")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
