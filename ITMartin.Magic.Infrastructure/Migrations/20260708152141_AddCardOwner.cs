using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Magic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCardOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Owner",
                table: "Cards");
        }
    }
}
