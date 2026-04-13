using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartinLibrary.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieMetadataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "Items",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Genre",
                table: "Items",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Plot",
                table: "Items",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReleaseYear",
                table: "Items",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Runtime",
                table: "Items",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Genre",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Plot",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ReleaseYear",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Runtime",
                table: "Items");
        }
    }
}
