using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Magic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Newproperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CopyrightYear",
                table: "Sets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrameStyle",
                table: "Sets",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SymbolColor",
                table: "Sets",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SymbolShape",
                table: "Sets",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CopyrightYear",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "FrameStyle",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "SymbolColor",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "SymbolShape",
                table: "Sets");
        }
    }
}
