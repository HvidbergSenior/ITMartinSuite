using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartinBudget.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addedmobilename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MobilePayName",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date_Amount_Description",
                table: "Transactions",
                columns: new[] { "Date", "Amount", "Description" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Date_Amount_Description",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "MobilePayName",
                table: "Transactions");
        }
    }
}
