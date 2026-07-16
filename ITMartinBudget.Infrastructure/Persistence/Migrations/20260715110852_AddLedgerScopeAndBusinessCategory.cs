using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartinBudget.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerScopeAndBusinessCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Date_Amount_NormalizedDescription",
                table: "Transactions");

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessCategory",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LedgerId",
                table: "Transactions",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "family");

            migrationBuilder.AddColumn<string>(
                name: "RawDetails",
                table: "Transactions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_LedgerId",
                table: "Transactions",
                column: "LedgerId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_LedgerId_Date_Amount_NormalizedDescription",
                table: "Transactions",
                columns: new[] { "LedgerId", "Date", "Amount", "NormalizedDescription" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_LedgerId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_LedgerId_Date_Amount_NormalizedDescription",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BusinessCategory",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "LedgerId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RawDetails",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date_Amount_NormalizedDescription",
                table: "Transactions",
                columns: new[] { "Date", "Amount", "NormalizedDescription" });
        }
    }
}
