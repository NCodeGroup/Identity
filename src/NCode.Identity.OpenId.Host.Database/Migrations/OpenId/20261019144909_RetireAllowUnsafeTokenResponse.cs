using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class RetireAllowUnsafeTokenResponse : Migration
    {
        // Every table that stores a settings document (ADR-0139: a stored document that names a retired setting fails
        // closed when it is read, so retiring a setting rewrites the stored documents).
        private static readonly string[] SettingsTables = ["Servers", "Tenants", "Clients", "ResourceServers"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 'allow_unsafe_token_response' is retired: 'response_types_supported' is the only control over the tokens
            // the authorization endpoint returns (ADR-0142). Each document is rewritten to the setting's meaning:
            //
            // - 'true' (or null, its default) restricted nothing, so the key is removed.
            // - 'false' rejected every response type that includes the 'token' name. The document instead narrows
            //   'response_types_supported' — which merges by intersection, exactly as 'false' merged by conjunction —
            //   to the values it lists that exclude 'token', or, when it lists none, to every response type that
            //   excludes 'token' (an unset list is unrestricted).
            //
            // A document whose value for the setting is not a boolean or null already fails to load; it is left as is,
            // rather than guessing at its intent. A rewritten document gets a new settings concurrency token, as any
            // other settings write does.
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
                                WHEN source.document -> 'allow_unsafe_token_response' = 'false'::jsonb
                                    THEN (source.document - 'allow_unsafe_token_response') || jsonb_build_object(
                                        'response_types_supported',
                                        CASE
                                            WHEN source.document -> 'response_types_supported' IS NULL
                                                THEN '["code", "id_token", "code id_token"]'::jsonb
                                            WHEN jsonb_typeof(source.document -> 'response_types_supported') = 'array'
                                                THEN COALESCE(
                                                    (
                                                        SELECT jsonb_agg(element.value ORDER BY element.ordinality)
                                                        FROM jsonb_array_elements(source.document -> 'response_types_supported')
                                                            WITH ORDINALITY AS element(value, ordinality)
                                                        WHERE jsonb_typeof(element.value) <> 'string'
                                                            OR NOT ('token' = ANY (string_to_array(element.value #>> '{}', ' ')))
                                                    ),
                                                    '[]'::jsonb
                                                )
                                            ELSE source.document -> 'response_types_supported'
                                        END
                                    )
                                ELSE source.document - 'allow_unsafe_token_response'
                            END AS document
                        FROM (
                            SELECT "Id", "SettingsJson"::jsonb AS document
                            FROM "{{table}}"
                            WHERE strpos("SettingsJson", 'allow_unsafe_token_response') > 0
                        ) AS source
                        WHERE jsonb_typeof(source.document -> 'allow_unsafe_token_response') IN ('boolean', 'null')
                    ) AS migrated
                    WHERE target."Id" = migrated."Id";
                    """
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: a rewritten document is valid and means the same under the previous model, which
            // honors a narrowed 'response_types_supported' and defaults 'allow_unsafe_token_response' to true.
        }
    }
}