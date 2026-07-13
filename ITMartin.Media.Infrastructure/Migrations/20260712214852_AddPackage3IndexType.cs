using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Media.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackage3IndexType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IndexType",
                table: "Package3IndexStatuses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndexType",
                table: "Package3IndexStatuses");
        }
    }
}
