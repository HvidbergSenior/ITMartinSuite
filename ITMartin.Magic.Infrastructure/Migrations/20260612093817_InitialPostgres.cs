using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Magic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sets",
                columns: table => new
                {
                    SetCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReleaseYear = table.Column<int>(type: "integer", nullable: false),
                    SymbolDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SymbolKeywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HasSetSymbol = table.Column<bool>(type: "boolean", nullable: false),
                    UsesOldFrame = table.Column<bool>(type: "boolean", nullable: false),
                    UsesWhiteBorder = table.Column<bool>(type: "boolean", nullable: false),
                    UsesBlackBorder = table.Column<bool>(type: "boolean", nullable: false),
                    HasCollectorNumbers = table.Column<bool>(type: "boolean", nullable: false),
                    HasFoils = table.Column<bool>(type: "boolean", nullable: false),
                    CopyrightStyle = table.Column<string>(type: "text", nullable: false),
                    SymbolColor = table.Column<string>(type: "text", nullable: false),
                    FrameStyle = table.Column<string>(type: "text", nullable: false),
                    CopyrightYear = table.Column<int>(type: "integer", nullable: true),
                    SymbolShape = table.Column<string>(type: "text", nullable: false)
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
