using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Playground.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddGrantRefreshTokenLineageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenLineageId",
                table: "Grants",
                type: "character varying(300)",
                unicode: false,
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grants_TenantId_RefreshTokenLineageId",
                table: "Grants",
                columns: new[] { "TenantId", "RefreshTokenLineageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Grants_TenantId_RefreshTokenLineageId",
                table: "Grants");

            migrationBuilder.DropColumn(
                name: "RefreshTokenLineageId",
                table: "Grants");
        }
    }
}
