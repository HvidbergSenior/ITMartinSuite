using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartinBudget.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 2026-11-18 "Sortér betalinger" (user: Eigil buys groceries and requests the money afterwards): a posting sorted by hand
    /// keeps its category - a payee rule (Flyt "alle til samme modtager") no longer overwrites it.
    /// </summary>
    [DbContext(typeof(BudgetDbContext))]
    [Migration("20261118230000_AddCategoryByHand")]
    public partial class AddCategoryByHand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CategoryByHand",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CategoryByHand", table: "Transactions");
        }
    }
}
