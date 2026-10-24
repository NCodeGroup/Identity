using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    [ExpandMigration(
        "Each removed setting sat at a scope the previous release never read it from."
    )]
    public partial class RemoveSettingsOutsideTheirScopes : Migration
    {
        // The settings a client's effective settings never read: discovery and the tenant-wide services read them from
        // a tenant's effective settings only, so a client document that carries one stored a setting the server never
        // honored (ADR-0149).
        private static readonly string[] SettingsOutsideClients =
        [
            "authorization_response_iss_parameter_supported", "backchannel_logout_session_supported",
            "backchannel_logout_supported", "backchannel_user_code_parameter_supported", "claim_types_supported",
            "default_acr", "federated_identity_explicit_only", "federated_identity_join_claim",
            "federated_identity_require_verified", "federated_identity_verified_claim",
            "frontchannel_logout_session_supported", "frontchannel_logout_supported", "max_concurrent_sessions",
            "mtls_endpoint_alias_authority", "op_policy_uri", "op_tos_uri", "principal_issuer_claim",
            "registration_endpoint_allow_anonymous", "request_object_encryption_alg_values_supported",
            "request_object_encryption_enc_values_supported", "request_object_encryption_zip_values_supported",
            "request_object_signing_alg_values_supported", "require_request_uri_registration",
            "sector_identifier_uri_allowed_hosts", "sector_identifier_uri_fetch_timeout",
            "sector_identifier_uri_max_size_bytes", "send_profile_metadata_in_user_info",
            "send_system_metadata_in_user_info", "service_documentation", "session_absolute_lifetime",
            "session_idle_timeout", "session_management_supported", "subject_claim_types", "subject_types_supported",
            "ui_locales_supported", "userinfo_encryption_alg_values_supported",
            "userinfo_encryption_enc_values_supported", "userinfo_encryption_zip_values_supported",
            "userinfo_signing_alg_values_supported",
        ];

        // The settings a resource server's effective settings never read: access-token issuance reads a targeted
        // resource server's settings only for its encryption, so a resource-server document that carries any other
        // setting stored one the server never honored (ADR-0149).
        private static readonly string[] SettingsOutsideResourceServers =
        [
            "access_token_lifetime", "access_token_signing_alg_values_supported", "access_token_type",
            "acr_values_supported", "allow_loopback_redirect", "allowed_identity_providers",
            "always_include_auth_time", "authorization_authenticate_scheme", "authorization_challenge_scheme",
            "authorization_code_lifetime", "authorization_response_iss_parameter_supported",
            "backchannel_authentication_request_encryption_alg_values_supported",
            "backchannel_authentication_request_encryption_enc_values_supported",
            "backchannel_authentication_request_encryption_zip_values_supported",
            "backchannel_authentication_request_signing_alg_values_supported", "backchannel_logout_session_required",
            "backchannel_logout_session_supported", "backchannel_logout_supported", "backchannel_logout_uri",
            "backchannel_token_delivery_mode", "backchannel_token_delivery_modes_supported",
            "backchannel_user_code_parameter_supported", "ciba_polling_interval", "ciba_request_lifetime",
            "claim_types_supported", "claims_locales_supported", "claims_parameter_supported", "claims_supported",
            "claims_supported_is_strict", "client_assertion_max_lifetime", "client_name",
            "client_notification_endpoint", "client_uri", "clock_skew", "code_challenge_methods_supported",
            "contacts", "continue_authorization_lifetime", "default_acr", "device_code_lifetime",
            "device_code_polling_interval", "display_values_supported", "dpop_nonce_required", "dpop_proof_lifetime",
            "dpop_signing_alg_values_supported", "federated_identity_explicit_only", "federated_identity_join_claim",
            "federated_identity_require_verified", "federated_identity_verified_claim",
            "frontchannel_logout_session_required", "frontchannel_logout_session_supported",
            "frontchannel_logout_supported", "frontchannel_logout_uri", "grant_types_supported",
            "id_token_encryption_alg_values_supported", "id_token_encryption_enc_values_supported",
            "id_token_encryption_required", "id_token_encryption_zip_values_supported", "id_token_lifetime",
            "id_token_signing_alg_values_supported", "jwks", "jwks_uri", "jwks_uri_allowed_hosts",
            "jwks_uri_cache_duration", "jwks_uri_fetch_timeout", "jwks_uri_max_size_bytes", "logo_uri",
            "max_concurrent_sessions", "mtls_endpoint_alias_authority", "op_policy_uri", "op_tos_uri",
            "pairwise_salt", "pkce_nonce_exemption", "policy_uri", "post_logout_redirect_uris",
            "principal_issuer_claim", "prompt_values_supported", "pushed_authorization_request_lifetime",
            "redirect_uris", "refresh_token_expiration_policy", "refresh_token_lifetime",
            "refresh_token_reuse_policy", "refresh_token_rotation_enabled", "registration_endpoint_allow_anonymous",
            "request_object_encryption_alg_values_supported", "request_object_encryption_enc_values_supported",
            "request_object_encryption_zip_values_supported", "request_object_expected_audience",
            "request_object_signing_alg_values_supported", "request_parameter_supported",
            "request_uri_allowed_hosts", "request_uri_expected_content_type", "request_uri_fetch_timeout",
            "request_uri_max_size_bytes", "request_uri_parameter_supported",
            "request_uri_require_strict_content_type", "require_consent", "require_dpop", "require_pkce",
            "require_pushed_authorization_requests", "require_request_object", "require_request_uri_registration",
            "response_modes_supported", "response_types_supported", "sector_identifier_uri",
            "sector_identifier_uri_allowed_hosts", "sector_identifier_uri_fetch_timeout",
            "sector_identifier_uri_max_size_bytes", "security_profiles", "send_id_claims_in_access_token",
            "send_profile_metadata_in_access_token", "send_profile_metadata_in_id_token",
            "send_profile_metadata_in_user_info", "send_system_metadata_in_access_token",
            "send_system_metadata_in_id_token", "send_system_metadata_in_user_info", "service_documentation",
            "session_absolute_lifetime", "session_idle_timeout", "session_management_supported", "software_id",
            "software_version", "subject_claim_types", "subject_max_age", "subject_type", "subject_types_supported",
            "tls_client_auth_san_dns", "tls_client_auth_san_email", "tls_client_auth_san_ip",
            "tls_client_auth_san_uri", "tls_client_auth_subject_dn", "tls_client_certificate_bound_access_tokens",
            "token_endpoint_auth_method", "token_endpoint_auth_methods_supported",
            "token_endpoint_auth_signing_alg_values_supported", "token_exchange_allow_refresh_token", "tos_uri",
            "ui_locales_supported", "user_code_length", "userinfo_encryption_alg_values_supported",
            "userinfo_encryption_enc_values_supported", "userinfo_encryption_zip_values_supported",
            "userinfo_signing_alg_values_supported",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A setting stored at a scope that never reads it now fails closed when the document is loaded (ADR-0149).
            // It never had an effect there, so it is removed rather than translated: no setting at that scope expressed
            // its intent, and moving it to the tenant would change every other client or resource server of the tenant.
            // Its value is irrelevant, so a document is rewritten whatever the value; any other setting the document
            // carries that cannot be honored still fails closed. Every setting applies within the server and within a
            // tenant, so the 'Servers' and 'Tenants' documents are unchanged. A rewritten document gets a new settings
            // concurrency token, as any other settings write does.
            RemoveSettings(migrationBuilder, "Clients", SettingsOutsideClients);
            RemoveSettings(migrationBuilder, "ResourceServers", SettingsOutsideResourceServers);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: a removed setting had no effect where it was stored, so the previous model behaves the
            // same without it.
        }

        private static void RemoveSettings(
            MigrationBuilder migrationBuilder,
            string table,
            string[] settingNames
        )
        {
            var names = string.Join(", ", settingNames.Select(name => $"'{name}'"));
            migrationBuilder.Sql(
                $$"""
                UPDATE "{{table}}"
                SET "SettingsJson" = ("SettingsJson"::jsonb - ARRAY[{{names}}]::text[])::text,
                    "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                WHERE jsonb_typeof("SettingsJson"::jsonb) = 'object'
                    AND ("SettingsJson"::jsonb - ARRAY[{{names}}]::text[]) <> "SettingsJson"::jsonb;
                """
            );
        }
    }
}