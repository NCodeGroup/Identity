using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddGrantTypeRevokedWhenExpiresWhenIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Grants_GrantType_RevokedWhen_ExpiresWhen",
                table: "Grants",
                columns: new[] { "GrantType", "RevokedWhen", "ExpiresWhen" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Grants_GrantType_RevokedWhen_ExpiresWhen",
                table: "Grants");
        }
    }
}
