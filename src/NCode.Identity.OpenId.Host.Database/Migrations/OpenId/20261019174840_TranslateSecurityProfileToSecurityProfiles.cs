using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    [ContractMigration(
        "Shipped before any release, so no running release reads 'security_profile' (ADR-0148, ADR-0025)."
    )]
    public partial class TranslateSecurityProfileToSecurityProfiles : Migration
    {
        // Every table that stores a settings document (ADR-0139: a stored document that names a retired setting fails
        // closed when it is read, so renaming a setting rewrites the stored documents).
        private static readonly string[] SettingsTables = ["Servers", "Tenants", "Clients", "ResourceServers"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 'security_profile' (one profile name) becomes 'security_profiles' (a list of profile names that
            // accumulates down the cascade, ADR-0148). Each document is rewritten to the setting's meaning:
            //
            // - A non-empty name selected that profile, so it becomes a list of that one name.
            // - An empty name (or null) selected no profile, which is the default, so the key is removed.
            //
            // A document whose value for the setting is not a string or null, or that already names
            // 'security_profiles', already fails to load; it is left as is, rather than guessing at its intent. A
            // rewritten document gets a new settings concurrency token, as any other settings write does.
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
                                WHEN jsonb_typeof(source.document -> 'security_profile') = 'string'
                                    AND source.document ->> 'security_profile' <> ''
                                    THEN (source.document - 'security_profile') || jsonb_build_object(
                                        'security_profiles',
                                        jsonb_build_array(source.document -> 'security_profile')
                                    )
                                ELSE source.document - 'security_profile'
                            END AS document
                        FROM (
                            SELECT "Id", "SettingsJson"::jsonb AS document
                            FROM "{{table}}"
                            WHERE strpos("SettingsJson", 'security_profile') > 0
                        ) AS source
                        WHERE jsonb_typeof(source.document -> 'security_profile') IN ('string', 'null')
                            AND source.document -> 'security_profiles' IS NULL
                    ) AS migrated
                    WHERE target."Id" = migrated."Id";
                    """
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous model selects at most one profile, so a list of no names removes the key and a list of one
            // name becomes that name. A list of several names has no equivalent there; it is left as is and fails
            // closed rather than being narrowed to one of its profiles.
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
                                WHEN jsonb_array_length(source.document -> 'security_profiles') = 1
                                    THEN (source.document - 'security_profiles') || jsonb_build_object(
                                        'security_profile',
                                        source.document -> 'security_profiles' -> 0
                                    )
                                ELSE source.document - 'security_profiles'
                            END AS document
                        FROM (
                            SELECT "Id", "SettingsJson"::jsonb AS document
                            FROM "{{table}}"
                            WHERE strpos("SettingsJson", 'security_profiles') > 0
                        ) AS source
                        WHERE jsonb_typeof(source.document -> 'security_profiles') = 'array'
                            AND jsonb_array_length(source.document -> 'security_profiles') <= 1
                            AND source.document -> 'security_profile' IS NULL
                    ) AS migrated
                    WHERE target."Id" = migrated."Id";
                    """
                );
            }
        }
    }
}