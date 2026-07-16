using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartinBudget.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LedgerConfigs",
                columns: table => new
                {
                    LedgerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ScopeMode = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerConfigs", x => x.LedgerId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LedgerConfigs");
        }
    }
}
