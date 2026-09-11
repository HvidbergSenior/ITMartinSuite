using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Media.Infrastructure.Migrations
{
    // Written by hand - `dotnet ef` is broken on the dev box (hostfxr) and this
    // is one nullable column. See PersonEntity.BornYear for why it exists.
    [DbContext(typeof(Persistence.MediaDbContext))]
    [Migration("20260911090000_AddPersonBornYear")]
    public partial class AddPersonBornYear : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BornYear",
                table: "People",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BornYear",
                table: "People");
        }
    }
}
