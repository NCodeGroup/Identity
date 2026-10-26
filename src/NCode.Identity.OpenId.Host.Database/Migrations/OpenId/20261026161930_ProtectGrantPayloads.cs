using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    [ContractMigration(
        "Shipped before any release, so no running release reads Grants.PayloadJson (ADR-0265, ADR-0025)."
    )]
    public partial class ProtectGrantPayloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Grants\";");

            migrationBuilder.DropColumn(
                name: "PayloadJson",
                table: "Grants");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedPayload",
                table: "Grants",
                type: "text",
                unicode: false,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Grants\";");

            migrationBuilder.DropColumn(
                name: "ProtectedPayload",
                table: "Grants");

            migrationBuilder.AddColumn<string>(
                name: "PayloadJson",
                table: "Grants",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
