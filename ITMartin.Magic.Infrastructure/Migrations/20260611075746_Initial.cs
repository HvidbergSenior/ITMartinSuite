using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Magic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sets",
                columns: table => new
                {
                    SetCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SetName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SymbolDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SymbolKeywords = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReleaseYear = table.Column<int>(type: "INTEGER", nullable: false),
                    UsesOldFrame = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsesWhiteBorder = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsesBlackBorder = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasCollectorNumbers = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasFoils = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasSetSymbol = table.Column<bool>(type: "INTEGER", nullable: false),
                    CopyrightStyle = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sets", x => x.SetCode);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sets");
        }
    }
}
