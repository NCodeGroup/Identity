using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    [ExpandMigration(
        "Each rewritten document is valid for the previous release, and its clients keep their methods."
    )]
    public partial class RegisterClientAuthenticationMethod : Migration
    {
        // Every table that stores a settings document (ADR-0140: narrowing a setting's domain makes a stored document
        // that uses the removed value fail closed when it is read, so the narrowing rewrites the stored documents).
        private static readonly string[] SettingsTables = ["Servers", "Tenants", "Clients", "ResourceServers"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 'token_endpoint_auth_method' is the one method a client authenticates with, and a client that registers
            // none uses the RFC 7591 default, 'client_secret_basic' (ADR-0145). The empty string, which registered no
            // method, is no longer a value of the setting. Each document that registers no method is rewritten to the
            // setting's meaning:
            //
            // - A document whose 'token_endpoint_auth_methods_supported' lists exactly one method let its clients
            //   authenticate with that method only, so it registers that method. This is how a public client was
            //   pinned to 'none' before it could register it.
            // - Any other document with an empty method has the key removed, so it uses the default.
            //
            // A document whose values for these settings have another shape is left as is, rather than guessing at its
            // intent; one that is invalid already fails to load. A rewritten document gets a new settings concurrency
            // token, as any other settings write does.
            foreach (var table in SettingsTables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}" AS target
                    SET "SettingsJson" = migrated.document::text,
                        "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                    FROM (
                        SELECT source."Id",
                            CASE
                                WHEN source.single_method IS NOT NULL
                                    THEN jsonb_set(source.document, '{token_endpoint_auth_method}', source.single_method)
                                ELSE source.document - 'token_endpoint_auth_method'
                            END AS document
                        FROM (
                            SELECT "Id",
                                "SettingsJson"::jsonb AS document,
                                CASE
                                    WHEN jsonb_typeof("SettingsJson"::jsonb -> 'token_endpoint_auth_methods_supported') = 'array'
                                        AND jsonb_array_length("SettingsJson"::jsonb -> 'token_endpoint_auth_methods_supported') = 1
                                        AND jsonb_typeof("SettingsJson"::jsonb -> 'token_endpoint_auth_methods_supported' -> 0) = 'string'
                                        THEN "SettingsJson"::jsonb -> 'token_endpoint_auth_methods_supported' -> 0
                                END AS single_method
                            FROM "{{table}}"
                            WHERE strpos("SettingsJson", 'token_endpoint_auth_method') > 0
                        ) AS source
                        WHERE source.document -> 'token_endpoint_auth_method' = '""'::jsonb
                            OR (
                                source.document -> 'token_endpoint_auth_method' IS NULL
                                AND source.single_method IS NOT NULL
                            )
                    ) AS migrated
                    WHERE target."Id" = migrated."Id";
                    """
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: a rewritten document is valid under the previous model, which accepted every method a
            // document registered, and its clients authenticate with the same methods as before.
        }
    }
}