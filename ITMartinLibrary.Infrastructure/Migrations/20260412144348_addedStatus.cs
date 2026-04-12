using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartinLibrary.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DetailsUpdatedAt",
                table: "Items",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LookupStatus",
                table: "Items",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetailsUpdatedAt",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "LookupStatus",
                table: "Items");
        }
    }
}
