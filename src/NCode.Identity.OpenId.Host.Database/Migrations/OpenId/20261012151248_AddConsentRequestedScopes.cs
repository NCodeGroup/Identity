using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddConsentRequestedScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestedScopesJson",
                table: "Consents",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedScopesJson",
                table: "Consents");
        }
    }
}
