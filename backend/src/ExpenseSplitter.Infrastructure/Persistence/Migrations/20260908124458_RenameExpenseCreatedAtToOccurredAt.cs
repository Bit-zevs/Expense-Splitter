using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseSplitter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameExpenseCreatedAtToOccurredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Expenses",
                newName: "OccurredAt");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_TripId_CreatedAt",
                table: "Expenses",
                newName: "IX_Expenses_TripId_OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OccurredAt",
                table: "Expenses",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Expenses_TripId_OccurredAt",
                table: "Expenses",
                newName: "IX_Expenses_TripId_CreatedAt");
        }
    }
}
