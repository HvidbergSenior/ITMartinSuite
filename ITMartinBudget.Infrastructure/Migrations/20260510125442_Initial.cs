using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartinBudget.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchType = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    NormalizedDescription = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionType = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpenseType = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchedRuleId = table.Column<int>(type: "INTEGER", nullable: true),
                    NeedsReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCategorizedManually = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_CategoryRules_MatchedRuleId",
                        column: x => x.MatchedRuleId,
                        principalTable: "CategoryRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_IsActive",
                table: "CategoryRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_Pattern",
                table: "CategoryRules",
                column: "Pattern");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_Priority",
                table: "CategoryRules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Category",
                table: "Transactions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date_Amount_Description",
                table: "Transactions",
                columns: new[] { "Date", "Amount", "Description" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ImportedAt",
                table: "Transactions",
                column: "ImportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_MatchedRuleId",
                table: "Transactions",
                column: "MatchedRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_NeedsReview",
                table: "Transactions",
                column: "NeedsReview");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionType",
                table: "Transactions",
                column: "TransactionType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "CategoryRules");
        }
    }
}
