using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMartin.Magic.Infrastructure.Migrations
{
    public partial class AddPriceAlerts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardName = table.Column<string>(type: "text", nullable: false),
                    SetCode = table.Column<string>(type: "text", nullable: false),
                    OldPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    NewPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Delta = table.Column<decimal>(type: "numeric", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Dismissed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceAlerts", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PriceAlerts");
        }
    }
}
