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
            migrationBuilder.Sql(@"ALTER TABLE ""Sets"" ADD COLUMN IF NOT EXISTS ""CopyrightYear"" INTEGER;");
            migrationBuilder.Sql(@"ALTER TABLE ""Sets"" ADD COLUMN IF NOT EXISTS ""FrameStyle"" TEXT NOT NULL DEFAULT '';");
            migrationBuilder.Sql(@"ALTER TABLE ""Sets"" ADD COLUMN IF NOT EXISTS ""SymbolColor"" TEXT NOT NULL DEFAULT '';");
            migrationBuilder.Sql(@"ALTER TABLE ""Sets"" ADD COLUMN IF NOT EXISTS ""SymbolShape"" TEXT NOT NULL DEFAULT '';");
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
