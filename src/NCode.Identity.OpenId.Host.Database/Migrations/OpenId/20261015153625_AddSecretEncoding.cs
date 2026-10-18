using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class AddSecretEncoding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecretEncoding",
                table: "Secrets",
                type: "character varying(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows to the stored-form axis (ADR-0104): symmetric raw bytes, X.509 certificates, or
            // (for rsa/ecc) private PKCS#8 — the only forms produced before public-key import existed.
            migrationBuilder.Sql(
                """
                UPDATE "Secrets"
                SET "SecretEncoding" = CASE "SecretType"
                    WHEN 'symmetric' THEN 'symmetric_raw'
                    WHEN 'x509' THEN 'x509'
                    ELSE 'pkcs8'
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecretEncoding",
                table: "Secrets");
        }
    }
}
