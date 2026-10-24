using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class RetireTenantIdentifiersAndIssuers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetiredIssuers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    NormalizedIssuer = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetiredIssuers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetiredTenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetiredTenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetiredIssuers_NormalizedIssuer",
                table: "RetiredIssuers",
                column: "NormalizedIssuer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetiredTenants_NormalizedTenantId",
                table: "RetiredTenants",
                column: "NormalizedTenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetiredIssuers");

            migrationBuilder.DropTable(
                name: "RetiredTenants");
        }
    }
}
