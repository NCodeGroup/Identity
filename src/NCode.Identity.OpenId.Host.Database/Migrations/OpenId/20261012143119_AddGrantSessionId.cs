using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddGrantSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "Grants",
                type: "character varying(300)",
                unicode: false,
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grants_TenantId_SessionId",
                table: "Grants",
                columns: new[] { "TenantId", "SessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Grants_TenantId_SessionId",
                table: "Grants");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Grants");
        }
    }
}
