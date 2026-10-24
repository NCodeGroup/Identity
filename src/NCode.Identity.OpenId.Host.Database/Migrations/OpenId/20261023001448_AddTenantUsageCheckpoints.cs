using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddTenantUsageCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantUsageCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ReplicaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastBatchNumber = table.Column<long>(type: "bigint", nullable: false),
                    LastDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsageCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsageCheckpoints_LastDay",
                table: "TenantUsageCheckpoints",
                column: "LastDay");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsageCheckpoints_ReplicaId",
                table: "TenantUsageCheckpoints",
                column: "ReplicaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantUsageCheckpoints");
        }
    }
}
