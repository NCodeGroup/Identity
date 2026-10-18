using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Playground.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class ReplaceTenantDomainNameWithIssuerHost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_NormalizedDomainName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DomainName",
                table: "Tenants");

            migrationBuilder.RenameColumn(
                name: "NormalizedDomainName",
                table: "Tenants",
                newName: "NormalizedIssuerHost");

            // The renamed column still holds the old domain names, which are no longer derived from anything: a tenant
            // could be routed to by a host it never claims as its issuer. Clear them so the column is only ever
            // written from an issuer. Operators re-establish host routing by setting each tenant's issuer.
            migrationBuilder.Sql("UPDATE \"Tenants\" SET \"NormalizedIssuerHost\" = NULL;");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedIssuer",
                table: "Tenants",
                type: "character varying(1000)",
                unicode: false,
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedIssuer",
                table: "Tenants",
                column: "NormalizedIssuer",
                unique: true,
                filter: "\"NormalizedIssuer\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedIssuerHost",
                table: "Tenants",
                column: "NormalizedIssuerHost",
                filter: "\"NormalizedIssuerHost\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_NormalizedIssuer",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_NormalizedIssuerHost",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NormalizedIssuer",
                table: "Tenants");

            migrationBuilder.RenameColumn(
                name: "NormalizedIssuerHost",
                table: "Tenants",
                newName: "NormalizedDomainName");

            migrationBuilder.AddColumn<string>(
                name: "DomainName",
                table: "Tenants",
                type: "character varying(300)",
                unicode: false,
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedDomainName",
                table: "Tenants",
                column: "NormalizedDomainName",
                unique: true,
                filter: "\"NormalizedDomainName\" IS NOT NULL");
        }
    }
}
