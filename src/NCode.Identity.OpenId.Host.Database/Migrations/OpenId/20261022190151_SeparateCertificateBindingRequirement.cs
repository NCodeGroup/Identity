using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class SeparateCertificateBindingRequirement : Migration
    {
        // The setting that now means the server supports certificate-bound access tokens (RFC 8705 section 3.3). It used
        // to be a client's requirement as well.
        private const string BoundTokens = "tls_client_certificate_bound_access_tokens";

        // The setting that is a client's requirement for certificate-bound access tokens (RFC 8705 section 3.4).
        private const string RequireBoundTokens = "require_tls_client_certificate_bound_access_tokens";

        // The setting that selects how a client certificate's revocation status is checked.
        private const string RevocationMode = "tls_client_auth_revocation_mode";

        // The tables whose settings documents hold a value for every client below them, where the old
        // 'tls_client_certificate_bound_access_tokens' also forced every one of those clients.
        private static readonly string[] ServerAndTenantTables = ["Servers", "Tenants"];

        // Every table that stores a settings document that can name 'tls_client_auth_revocation_mode'.
        private static readonly string[] RevocationModeTables = ["Servers", "Tenants", "Clients"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous release keeps serving while this migration is applied (ADR-0180), and ADR-0184 leaves a
            // data rewrite to review. That release fails closed on a client whose document now carries
            // 'require_tls_client_certificate_bound_access_tokens', because a stored document that names a setting the
            // release does not know fails to load (ADR-0139). Such a client is refused until the rollout completes.
            //
            // Every rewritten document gets a new settings concurrency token, as any other settings write does. A
            // document that is not a JSON object is left as is, rather than guessing at its intent.

            // 1. A client's 'tls_client_certificate_bound_access_tokens' was its requirement, so it moves to
            // 'require_tls_client_certificate_bound_access_tokens'. A client's document that already names the
            // requirement is left as is.
            migrationBuilder.Sql(
                $$"""
                UPDATE "Clients" AS target
                SET "SettingsJson" = (
                        (source.document - '{{BoundTokens}}')
                        || jsonb_build_object('{{RequireBoundTokens}}', source.document -> '{{BoundTokens}}')
                    )::text,
                    "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                FROM (
                    SELECT "Id", "SettingsJson"::jsonb AS document
                    FROM "Clients"
                    WHERE strpos("SettingsJson", '{{BoundTokens}}') > 0
                ) AS source
                WHERE target."Id" = source."Id"
                    AND jsonb_typeof(source.document) = 'object'
                    AND jsonb_typeof(source.document -> '{{BoundTokens}}') = 'boolean'
                    AND source.document -> '{{RequireBoundTokens}}' IS NULL;
                """
            );

            // 2. On a server or a tenant, 'tls_client_certificate_bound_access_tokens' set to true also required every
            // client below it to bind its access tokens. That requirement is kept, now explicit as
            // 'require_tls_client_certificate_bound_access_tokens', rather than silently loosened. The old key stays
            // and now only states the support.
            foreach (var table in ServerAndTenantTables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}" AS target
                    SET "SettingsJson" = (source.document || jsonb_build_object('{{RequireBoundTokens}}', true))::text,
                        "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                    FROM (
                        SELECT "Id", "SettingsJson"::jsonb AS document
                        FROM "{{table}}"
                        WHERE strpos("SettingsJson", '{{BoundTokens}}') > 0
                    ) AS source
                    WHERE target."Id" = source."Id"
                        AND jsonb_typeof(source.document) = 'object'
                        AND source.document -> '{{BoundTokens}}' = 'true'::jsonb
                        AND source.document -> '{{RequireBoundTokens}}' IS NULL;
                    """
                );
            }

            // 3. 'tls_client_auth_revocation_mode' of 'offline' has no meaning now that the server owns retrieval of
            // revocation data, so it becomes 'online', the default.
            foreach (var table in RevocationModeTables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}" AS target
                    SET "SettingsJson" = jsonb_set(source.document, ARRAY['{{RevocationMode}}'], '"online"'::jsonb)::text,
                        "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                    FROM (
                        SELECT "Id", "SettingsJson"::jsonb AS document
                        FROM "{{table}}"
                        WHERE strpos("SettingsJson", '{{RevocationMode}}') > 0
                    ) AS source
                    WHERE target."Id" = source."Id"
                        AND jsonb_typeof(source.document) = 'object'
                        AND source.document ->> '{{RevocationMode}}' = 'offline';
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
            // 'tls_client_auth_revocation_mode' is not restored to 'offline', because the previous model accepts
            // 'online', which is what the 'Up' rewrite stored.

            // 1. A client's 'require_tls_client_certificate_bound_access_tokens' moves back to its old
            // 'tls_client_certificate_bound_access_tokens', which was its requirement. A client's document that already
            // names the old key is left as is.
            migrationBuilder.Sql(
                $$"""
                UPDATE "Clients" AS target
                SET "SettingsJson" = (
                        (source.document - '{{RequireBoundTokens}}')
                        || jsonb_build_object('{{BoundTokens}}', source.document -> '{{RequireBoundTokens}}')
                    )::text,
                    "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                FROM (
                    SELECT "Id", "SettingsJson"::jsonb AS document
                    FROM "Clients"
                    WHERE strpos("SettingsJson", '{{RequireBoundTokens}}') > 0
                ) AS source
                WHERE target."Id" = source."Id"
                    AND jsonb_typeof(source.document) = 'object'
                    AND jsonb_typeof(source.document -> '{{RequireBoundTokens}}') = 'boolean'
                    AND source.document -> '{{BoundTokens}}' IS NULL;
                """
            );

            // 2. The previous model expresses a server's or a tenant's requirement for every client below it only
            // through the old 'tls_client_certificate_bound_access_tokens', so a requirement of true becomes that key
            // set to true. A requirement of false has no equivalent there and is dropped. The old key, which states
            // the support, is left as it is, and the previous release reads a value of true there as a requirement
            // for every client below it, so a server or tenant that advertised support without requiring binding
            // requires it after a rollback. That is stricter, never looser. The new key is removed either way,
            // because the previous release does not know it.
            foreach (var table in ServerAndTenantTables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}" AS target
                    SET "SettingsJson" = (
                            (source.document - '{{RequireBoundTokens}}')
                            || CASE
                                WHEN source.document -> '{{RequireBoundTokens}}' = 'true'::jsonb
                                    THEN jsonb_build_object('{{BoundTokens}}', true)
                                ELSE '{}'::jsonb
                            END
                        )::text,
                        "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                    FROM (
                        SELECT "Id", "SettingsJson"::jsonb AS document
                        FROM "{{table}}"
                        WHERE strpos("SettingsJson", '{{RequireBoundTokens}}') > 0
                    ) AS source
                    WHERE target."Id" = source."Id"
                        AND jsonb_typeof(source.document) = 'object'
                        AND source.document -> '{{RequireBoundTokens}}' IS NOT NULL;
                    """
                );
            }
        }
    }
}
