using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseSplitter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Trips",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "RUB");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_Currency",
                table: "Trips",
                sql: "\"Currency\" IN ('RUB', 'EUR', 'USD')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_Currency",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Trips");
        }
    }
}
