using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class PromoteMtlsEndpointAliasAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MtlsEndpointAliasAuthority",
                table: "Tenants",
                type: "character varying(1000)",
                unicode: false,
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedMtlsEndpointAliasHost",
                table: "Tenants",
                type: "character varying(300)",
                unicode: false,
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_NormalizedMtlsEndpointAliasHost",
                table: "Tenants",
                column: "NormalizedMtlsEndpointAliasHost",
                filter: "\"NormalizedMtlsEndpointAliasHost\" IS NOT NULL");

            // The 'mtls_endpoint_alias_authority' setting is retired: the alias authority is a tenant property the host
            // strategy routes on (ADR-0195). Each tenant's stored value moves into the new column, with its derived host,
            // and the key leaves the settings document, which gets a new settings concurrency token as any other settings
            // write does (ADR-0139: a stored document that names a retired setting fails closed when it is read). The
            // setting applied only to tenants, so no other table stores it. A stored value was validated as an https
            // origin when it was written; an empty one meant "no aliases".
            migrationBuilder.Sql(
                """
                UPDATE "Tenants" AS target
                SET "MtlsEndpointAliasAuthority" = migrated.authority,
                    "NormalizedMtlsEndpointAliasHost" = lower(
                        substring(migrated.authority from '^https://(\[[^]]+\]|[^/:]+)')
                    ),
                    "SettingsJson" = (migrated.document - 'mtls_endpoint_alias_authority')::text,
                    "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                FROM (
                    SELECT source."Id",
                        source.document,
                        NULLIF(rtrim(source.document ->> 'mtls_endpoint_alias_authority', '/'), '') AS authority
                    FROM (
                        SELECT "Id", "SettingsJson"::jsonb AS document
                        FROM "Tenants"
                        WHERE strpos("SettingsJson", 'mtls_endpoint_alias_authority') > 0
                    ) AS source
                    WHERE jsonb_typeof(source.document -> 'mtls_endpoint_alias_authority') IN ('string', 'null')
                ) AS migrated
                WHERE target."Id" = migrated."Id";
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_NormalizedMtlsEndpointAliasHost",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MtlsEndpointAliasAuthority",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NormalizedMtlsEndpointAliasHost",
                table: "Tenants");
        }
    }
}
