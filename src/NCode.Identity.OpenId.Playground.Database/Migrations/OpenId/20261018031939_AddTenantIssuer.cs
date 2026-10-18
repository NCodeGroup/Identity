using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Playground.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddTenantIssuer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Issuer",
                table: "Tenants",
                type: "character varying(1000)",
                unicode: false,
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Issuer",
                table: "Tenants");
        }
    }
}
