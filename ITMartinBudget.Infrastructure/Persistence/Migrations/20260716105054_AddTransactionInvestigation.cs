using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartinBudget.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionInvestigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionInvestigations",
                columns: table => new
                {
                    LedgerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Reasoning = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedScope = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionInvestigations", x => new { x.LedgerId, x.Pattern });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionInvestigations");
        }
    }
}
