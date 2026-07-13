using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Media.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSceneSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaDescriptions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaDescriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    MediaFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaDescriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaDescriptions_MediaFilePath",
                table: "MediaDescriptions",
                column: "MediaFilePath",
                unique: true);
        }
    }
}
