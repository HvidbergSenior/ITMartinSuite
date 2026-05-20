using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Media.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "Package1Manifests",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", nullable: false),
                    FileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaFilesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Package1Manifests", x => x.WorkflowId);
                });

            migrationBuilder.CreateTable(
                name: "ScanSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TotalFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowName = table.Column<string>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: false),
                    StateJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentStep = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.WorkflowId);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStateSnapshots",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SerializedContext = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStateSnapshots", x => x.WorkflowId);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepExecutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepExecutions_WorkflowId_StepName_Status",
                table: "WorkflowStepExecutions",
                columns: new[] { "WorkflowId", "StepName", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiCache");

            migrationBuilder.DropTable(
                name: "Package1Manifests");

            migrationBuilder.DropTable(
                name: "ScanSessions");

            migrationBuilder.DropTable(
                name: "WorkflowCheckpoints");

            migrationBuilder.DropTable(
                name: "WorkflowInstances");

            migrationBuilder.DropTable(
                name: "WorkflowStateSnapshots");

            migrationBuilder.DropTable(
                name: "WorkflowStepExecutions");
        }
    }
}
