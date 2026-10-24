using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    [ExpandMigration(
        "The copy writes only columns the previous release does not read and keeps the setting it reads."
    )]
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
            // strategy routes on (ADR-0195). Each tenant's stored value is copied into the alias columns, with its
            // derived host, and the tenant gets a new concurrency token, as any other tenant write does. The setting
            // stays in the settings document: the previous release reads it while this release rolls out and after a
            // rollback (ADR-0180), this release ignores it as a retired setting, and a later release removes it
            // (ADR-0251). The setting applied only to tenants. A stored value was validated as an https origin when it
            // was written; an empty or null value meant "no aliases", so only a non-empty string is copied.
            migrationBuilder.Sql(
                """
                UPDATE "Tenants" AS target
                SET "MtlsEndpointAliasAuthority" = migrated.authority,
                    "NormalizedMtlsEndpointAliasHost" = lower(
                        substring(migrated.authority from '^https://(\[[^]]+\]|[^/:]+)')
                    ),
                    "ConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                FROM (
                    SELECT source."Id",
                        NULLIF(rtrim(source.document ->> 'mtls_endpoint_alias_authority', '/'), '') AS authority
                    FROM (
                        SELECT "Id", "SettingsJson"::jsonb AS document
                        FROM "Tenants"
                        WHERE strpos("SettingsJson", 'mtls_endpoint_alias_authority') > 0
                    ) AS source
                    WHERE jsonb_typeof(source.document) = 'object'
                        AND jsonb_typeof(source.document -> 'mtls_endpoint_alias_authority') = 'string'
                ) AS migrated
                WHERE target."Id" = migrated."Id"
                    AND migrated.authority IS NOT NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous release reads the alias from the 'mtls_endpoint_alias_authority' setting, which Up leaves
            // in place. The setting takes each tenant's current alias, so an alias changed while this release served
            // survives the downgrade, and a tenant without an alias loses a stale value. A tenant with an alias and a
            // null settings document, as a provisioned tenant has, gets a document that holds only the setting. A
            // rewritten document gets a new settings concurrency token, as any other settings write does. Any other
            // document that is not a JSON object, and an empty, null or non-string value of a tenant without an
            // alias, are left as is.
            migrationBuilder.Sql(
                """
                UPDATE "Tenants"
                SET "SettingsJson" = CASE
                        WHEN "MtlsEndpointAliasAuthority" IS NULL
                            THEN ("SettingsJson"::jsonb - 'mtls_endpoint_alias_authority')::text
                        ELSE jsonb_set(
                            CASE
                                WHEN jsonb_typeof("SettingsJson"::jsonb) = 'null' THEN '{}'::jsonb
                                ELSE "SettingsJson"::jsonb
                            END,
                            '{mtls_endpoint_alias_authority}',
                            to_jsonb("MtlsEndpointAliasAuthority")
                        )::text
                    END,
                    "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                WHERE (
                        jsonb_typeof("SettingsJson"::jsonb) = 'object'
                        OR (jsonb_typeof("SettingsJson"::jsonb) = 'null' AND "MtlsEndpointAliasAuthority" IS NOT NULL)
                    )
                    AND CASE
                        WHEN "MtlsEndpointAliasAuthority" IS NULL
                            THEN jsonb_typeof("SettingsJson"::jsonb -> 'mtls_endpoint_alias_authority') = 'string'
                                AND "SettingsJson"::jsonb ->> 'mtls_endpoint_alias_authority' <> ''
                        ELSE ("SettingsJson"::jsonb -> 'mtls_endpoint_alias_authority')
                            IS DISTINCT FROM to_jsonb("MtlsEndpointAliasAuthority")
                    END;
                """
            );

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
