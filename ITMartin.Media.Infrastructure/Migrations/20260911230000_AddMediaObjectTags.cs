using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Media.Infrastructure.Migrations
{
    // Written by hand - `dotnet ef` is broken on the dev box (hostfxr). One
    // table: what the local object detector found in each photo. See
    // MediaObjectTagEntity.
    [DbContext(typeof(Persistence.MediaDbContext))]
    [Migration("20260911230000_AddMediaObjectTags")]
    public partial class AddMediaObjectTags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaObjectTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaObjectTags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaObjectTags_Label",
                table: "MediaObjectTags",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_MediaObjectTags_RelativePath",
                table: "MediaObjectTags",
                column: "RelativePath");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MediaObjectTags");
        }
    }
}
