using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class RenameRefreshTokenRotationSetting : Migration
    {
        // The setting that was a client's switch for rotating its refresh tokens.
        private const string OldName = "refresh_token_rotation_enabled";

        // The setting that is a client's refresh token rotation policy. The 'always' policy rotates every refresh
        // token, as the enabled switch did; the default 'unbound' policy rotates a refresh token that is bound neither
        // to a proof-of-possession key nor to client authentication.
        private const string NewName = "refresh_token_rotation_policy";

        // The policy that rotates every refresh token.
        private const string Always = "always";

        // Every table that stores a settings document that can name the setting. The setting applies to clients, and a
        // server or tenant document may carry it as a default for its clients.
        private static readonly string[] Tables = ["Servers", "Tenants", "Clients"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous release keeps serving while this migration is applied (ADR-0180), and ADR-0184 leaves a
            // data rewrite to review. That release fails closed on a document that carries
            // 'refresh_token_rotation_policy', because a stored document that names a setting the release does not
            // know fails to load (ADR-0139). Such a document is refused until the rollout completes.
            //
            // Every rewritten document gets a new settings concurrency token, as any other settings write does. A
            // document that is not a JSON object is left as is, rather than guessing at its intent.
            //
            // A 'true' becomes the 'always' policy. A 'false' is removed, because the default policy rotates the
            // refresh token of a public client that is not sender-constrained, and no setting turns that off.
            //
            // A document whose 'refresh_token_rotation_enabled' is not a boolean, or that already names
            // 'refresh_token_rotation_policy', is left as is, so it keeps failing closed rather than having one of
            // its two values silently dropped.
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}" AS target
                    SET "SettingsJson" = (
                            (source.document - '{{OldName}}')
                            || CASE
                                WHEN source.document -> '{{OldName}}' = 'true'::jsonb
                                THEN jsonb_build_object('{{NewName}}', '{{Always}}')
                                ELSE '{}'::jsonb
                            END
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
            // The 'always' policy becomes a 'true'. Any other policy is removed: the previous model cannot express
            // 'unbound', which is the default policy.
            //
            // A document whose 'refresh_token_rotation_policy' is not a string, or that already names
            // 'refresh_token_rotation_enabled', is left as is, so it keeps failing closed rather than having one of
            // its two values silently dropped.
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}" AS target
                    SET "SettingsJson" = (
                            (source.document - '{{NewName}}')
                            || CASE
                                WHEN source.document ->> '{{NewName}}' = '{{Always}}'
                                THEN jsonb_build_object('{{OldName}}', true)
                                ELSE '{}'::jsonb
                            END
                        )::text,
                        "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                    FROM (
                        SELECT "Id", "SettingsJson"::jsonb AS document
                        FROM "{{table}}"
                        WHERE strpos("SettingsJson", '{{NewName}}') > 0
                    ) AS source
                    WHERE target."Id" = source."Id"
                        AND jsonb_typeof(source.document) = 'object'
                        AND jsonb_typeof(source.document -> '{{NewName}}') = 'string'
                        AND source.document -> '{{OldName}}' IS NULL;
                    """
                );
            }
        }
    }
}
