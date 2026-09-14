using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Media.Infrastructure.Migrations
{
    // Written by hand - `dotnet ef` is broken on the dev box (hostfxr). One
    // nullable column; see PersonEntity.MatchThreshold.
    [DbContext(typeof(Persistence.MediaDbContext))]
    [Migration("20260914100000_AddPersonMatchThreshold")]
    public partial class AddPersonMatchThreshold : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MatchThreshold",
                table: "People",
                type: "REAL",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MatchThreshold", table: "People");
        }
    }
}
