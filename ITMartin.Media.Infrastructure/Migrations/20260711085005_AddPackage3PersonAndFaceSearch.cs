using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Media.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackage3PersonAndFaceSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaDescriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaDescriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaFaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "TEXT", nullable: false),
                    MatchedPersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    UserConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Package3IndexStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryPath = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TotalFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentFile = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Package3IndexStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonReferencePhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhotoPath = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonReferencePhotos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaDescriptions_MediaFilePath",
                table: "MediaDescriptions",
                column: "MediaFilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFaces_MatchedPersonId",
                table: "MediaFaces",
                column: "MatchedPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFaces_MediaFilePath",
                table: "MediaFaces",
                column: "MediaFilePath");

            migrationBuilder.CreateIndex(
                name: "IX_Package3IndexStatuses_LibraryPath",
                table: "Package3IndexStatuses",
                column: "LibraryPath");

            migrationBuilder.CreateIndex(
                name: "IX_PersonReferencePhotos_PersonId",
                table: "PersonReferencePhotos",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaDescriptions");

            migrationBuilder.DropTable(
                name: "MediaFaces");

            migrationBuilder.DropTable(
                name: "Package3IndexStatuses");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "PersonReferencePhotos");
        }
    }
}
