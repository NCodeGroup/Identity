using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddTenantUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantUsage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Meter = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsage_Day",
                table: "TenantUsage",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsage_TenantId_Day_Meter",
                table: "TenantUsage",
                columns: new[] { "TenantId", "Day", "Meter" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantUsage");
        }
    }
}
