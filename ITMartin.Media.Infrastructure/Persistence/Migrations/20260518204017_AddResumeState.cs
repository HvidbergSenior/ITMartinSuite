using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Media.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowCheckpoints_WorkflowId",
                table: "WorkflowCheckpoints");

            migrationBuilder.CreateTable(
                name: "AiCache",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCache", x => x.Hash);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowResumes",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastCompletedStep = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowResumes", x => x.WorkflowId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiCache");

            migrationBuilder.DropTable(
                name: "WorkflowResumes");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCheckpoints_WorkflowId",
                table: "WorkflowCheckpoints",
                column: "WorkflowId");
        }
    }
}
