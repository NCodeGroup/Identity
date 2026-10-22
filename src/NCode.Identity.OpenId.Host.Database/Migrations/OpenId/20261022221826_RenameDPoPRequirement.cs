using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class RenameDPoPRequirement : Migration
    {
        // The setting that was a client's requirement for DPoP-bound access tokens (RFC 9449).
        private const string OldName = "require_dpop";

        // The setting that is a client's requirement for DPoP-bound access tokens. It is named after the client
        // metadata member 'dpop_bound_access_tokens' (RFC 9449 section 5.2), as the certificate-bound requirement is.
        private const string NewName = "require_dpop_bound_access_tokens";

        // Every table that stores a settings document that can name the requirement. The setting applies to clients,
        // and a server or tenant document may carry it as a default for its clients.
        private static readonly string[] Tables = ["Servers", "Tenants", "Clients"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous release keeps serving while this migration is applied (ADR-0180), and ADR-0184 leaves a
            // data rewrite to review. That release fails closed on a document that now carries
            // 'require_dpop_bound_access_tokens', because a stored document that names a setting the release does not
            // know fails to load (ADR-0139). Such a document is refused until the rollout completes.
            //
            // Every rewritten document gets a new settings concurrency token, as any other settings write does. A
            // document that is not a JSON object is left as is, rather than guessing at its intent.
            //
            // A document whose 'require_dpop' is not a boolean, or that already names
            // 'require_dpop_bound_access_tokens', is left as is, so it keeps failing closed rather than having one of
            // its two values silently dropped.
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}" AS target
                    SET "SettingsJson" = (
                            (source.document - '{{OldName}}')
                            || jsonb_build_object('{{NewName}}', source.document -> '{{OldName}}')
                        )::text,
                        "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                    FROM (
                        SELECT "Id", "SettingsJson"::jsonb AS document
                        FROM "{{table}}"
                        WHERE strpos("SettingsJson", '{{OldName}}') > 0
                    ) AS source
                    WHERE target."Id" = source."Id"
                        AND jsonb_typeof(source.document) = 'object'
                        AND jsonb_typeof(source.document -> '{{OldName}}') = 'boolean'
                        AND source.document -> '{{NewName}}' IS NULL;
                    """
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Every rewritten document gets a new settings concurrency token, as any other settings write does. A
            // document that is not a JSON object is left as is, rather than guessing at its intent.
            //
            // A document whose 'require_dpop_bound_access_tokens' is not a boolean, or that already names
            // 'require_dpop', is left as is, so it keeps failing closed rather than having one of its two values
            // silently dropped.
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}" AS target
                    SET "SettingsJson" = (
                            (source.document - '{{NewName}}')
                            || jsonb_build_object('{{OldName}}', source.document -> '{{NewName}}')
                        )::text,
                        "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                    FROM (
                        SELECT "Id", "SettingsJson"::jsonb AS document
                        FROM "{{table}}"
                        WHERE strpos("SettingsJson", '{{NewName}}') > 0
                    ) AS source
                    WHERE target."Id" = source."Id"
                        AND jsonb_typeof(source.document) = 'object'
                        AND jsonb_typeof(source.document -> '{{NewName}}') = 'boolean'
                        AND source.document -> '{{OldName}}' IS NULL;
                    """
                );
            }
        }
    }
}
