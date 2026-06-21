using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Media.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgressCurrent",
                table: "WorkflowInstances",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressItem",
                table: "WorkflowInstances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressTotal",
                table: "WorkflowInstances",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgressCurrent",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "ProgressItem",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "ProgressTotal",
                table: "WorkflowInstances");
        }
    }
}
