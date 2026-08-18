using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ITMartinAdhd.Infrastructure.Persistence;

#nullable disable

namespace ITMartinAdhd.Infrastructure.Persistence.Migrations
{
    // Was missing the [DbContext]/[Migration] attributes that dotnet ef
    // normally generates in a companion .Designer.cs - without them EF's
    // migration scanner never discovered this migration at all, so
    // Migrate() silently applied only InitialCreate and PhotoPath never
    // got added to a fresh database.
    [DbContext(typeof(AdhdDbContext))]
    [Migration("20260622000000_AddPhotoPath")]
    /// <inheritdoc />
    public partial class AddPhotoPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "StoredItems",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "StoredItems");
        }
    }
}
