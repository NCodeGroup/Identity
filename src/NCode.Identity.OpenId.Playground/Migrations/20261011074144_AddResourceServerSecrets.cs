using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Playground.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceServerSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecretsConcurrencyToken",
                table: "ResourceServers",
                type: "TEXT",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SettingsConcurrencyToken",
                table: "ResourceServers",
                type: "TEXT",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ResourceServerSecrets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    NormalizedTenantId = table.Column<string>(type: "TEXT", unicode: false, maxLength: 300, nullable: false),
                    ResourceServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SecretId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceServerSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceServerSecrets_ResourceServers_ResourceServerId",
                        column: x => x.ResourceServerId,
                        principalTable: "ResourceServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceServerSecrets_Secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "Secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceServerSecrets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServerSecrets_ResourceServerId",
                table: "ResourceServerSecrets",
                column: "ResourceServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServerSecrets_SecretId",
                table: "ResourceServerSecrets",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServerSecrets_TenantId_ResourceServerId_SecretId",
                table: "ResourceServerSecrets",
                columns: new[] { "TenantId", "ResourceServerId", "SecretId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceServerSecrets");

            migrationBuilder.DropColumn(
                name: "SecretsConcurrencyToken",
                table: "ResourceServers");

            migrationBuilder.DropColumn(
                name: "SettingsConcurrencyToken",
                table: "ResourceServers");
        }
    }
}
