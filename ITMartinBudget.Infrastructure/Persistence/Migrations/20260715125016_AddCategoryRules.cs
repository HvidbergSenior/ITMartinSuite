using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartinBudget.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserCategoryName",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LedgerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_LedgerId_Pattern",
                table: "CategoryRules",
                columns: new[] { "LedgerId", "Pattern" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryRules");

            migrationBuilder.DropColumn(
                name: "UserCategoryName",
                table: "Transactions");
        }
    }
}
