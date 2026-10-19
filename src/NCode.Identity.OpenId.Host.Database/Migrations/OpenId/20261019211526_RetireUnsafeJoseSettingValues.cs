using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class RetireUnsafeJoseSettingValues : Migration
    {
        // Every table that stores a settings document (ADR-0139).
        private static readonly string[] SettingsTables =
        [
            "Servers", "Tenants", "Clients", "ResourceServers",
        ];

        // Retired settings (ADR-0155): the compression lists, because the server neither compresses what it encrypts nor
        // accepts a compressed JWT (RFC 8725 §3.6), and the CIBA request-object encryption lists, because CIBA defines no
        // encrypted authentication request (CIBA Core §7.1.1).
        private static readonly string[] RetiredSettings =
        [
            "access_token_encryption_zip_values_supported", "id_token_encryption_zip_values_supported",
            "userinfo_encryption_zip_values_supported", "request_object_encryption_zip_values_supported",
            "backchannel_authentication_request_encryption_alg_values_supported",
            "backchannel_authentication_request_encryption_enc_values_supported",
            "backchannel_authentication_request_encryption_zip_values_supported",
        ];

        // RSAES-PKCS1-v1_5 key encryption, which no algorithm setting offers (RFC 8725 §3.2).
        private static readonly string[] RsaPkcs1KeyManagement = ["RSA1_5"];

        // The key-management algorithms that need a key both parties share, which a response encrypted to a client's
        // public key and a request object encrypted to the server's public key cannot use (ADR-0155), plus RSA1_5.
        private static readonly string[] NonAsymmetricKeyManagement =
        [
            "dir", "A128KW", "A192KW", "A256KW", "A128GCMKW", "A192GCMKW", "A256GCMKW", "PBES2-HS256+A128KW",
            "PBES2-HS384+A192KW", "PBES2-HS512+A256KW", "RSA1_5",
        ];

        // The symmetric signature algorithms, which a signed CIBA authentication request cannot use (CIBA Core §7.1.1).
        private static readonly string[] SymmetricSignature = ["HS256", "HS384", "HS512"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A retired setting fails closed when a document that carries it is loaded (ADR-0139). Each one only enabled
            // something the server no longer does — compressing what it encrypts, accepting a compressed or an encrypted
            // CIBA request — and no setting expresses that intent any more, so it is removed rather than translated.
            //
            // A narrowed domain (ADR-0140) fails closed the same way. Each removed value names an algorithm the server
            // could never honor where the list applies — a response it addresses to a client is encrypted to the client's
            // public key, a request object to the server's, a CIBA request is signed asymmetrically — or one RFC 8725
            // §3.2 says to avoid, so removing it from the list narrows the list to what it could already permit; a list
            // left empty stays an empty list, which permits nothing, and never becomes an unset one, which is
            // unrestricted (ADR-0010). A value the server does not recognize is left in place and keeps failing closed.
            // A rewritten document gets a new settings concurrency token, as any other settings write does.
            foreach (var table in SettingsTables)
            {
                RemoveSettings(migrationBuilder, table, RetiredSettings);

                RemoveValues(
                    migrationBuilder,
                    table,
                    "access_token_encryption_alg_values_supported",
                    RsaPkcs1KeyManagement
                );
                RemoveValues(
                    migrationBuilder,
                    table,
                    "id_token_encryption_alg_values_supported",
                    NonAsymmetricKeyManagement
                );
                RemoveValues(
                    migrationBuilder,
                    table,
                    "userinfo_encryption_alg_values_supported",
                    NonAsymmetricKeyManagement
                );
                RemoveValues(
                    migrationBuilder,
                    table,
                    "request_object_encryption_alg_values_supported",
                    NonAsymmetricKeyManagement
                );
                RemoveValues(
                    migrationBuilder,
                    table,
                    "backchannel_authentication_request_signing_alg_values_supported",
                    SymmetricSignature
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: the previous model accepts every rewritten document, and a removed value or setting only
            // permitted what the server should not do.
        }

        private static string ToSqlList(string[] values) =>
            string.Join(", ", values.Select(value => $"'{value}'"));

        private static void RemoveSettings(
            MigrationBuilder migrationBuilder,
            string table,
            string[] settingNames
        )
        {
            var names = ToSqlList(settingNames);
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

        private static void RemoveValues(
            MigrationBuilder migrationBuilder,
            string table,
            string settingName,
            string[] removedValues
        )
        {
            var values = ToSqlList(removedValues);
            migrationBuilder.Sql(
                $$"""
                UPDATE "{{table}}"
                SET "SettingsJson" = jsonb_set(
                        "SettingsJson"::jsonb,
                        ARRAY['{{settingName}}'],
                        COALESCE(
                            (
                                SELECT jsonb_agg(item.value ORDER BY item.ordinality)
                                FROM jsonb_array_elements("SettingsJson"::jsonb -> '{{settingName}}')
                                    WITH ORDINALITY AS item(value, ordinality)
                                WHERE jsonb_typeof(item.value) <> 'string'
                                    OR (item.value #>> '{}') <> ALL (ARRAY[{{values}}]::text[])
                            ),
                            '[]'::jsonb
                        )
                    )::text,
                    "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                WHERE jsonb_typeof("SettingsJson"::jsonb) = 'object'
                    AND jsonb_typeof("SettingsJson"::jsonb -> '{{settingName}}') = 'array'
                    AND EXISTS (
                        SELECT 1
                        FROM jsonb_array_elements("SettingsJson"::jsonb -> '{{settingName}}') AS item(value)
                        WHERE jsonb_typeof(item.value) = 'string'
                            AND (item.value #>> '{}') = ANY (ARRAY[{{values}}]::text[])
                    );
                """
            );
        }
    }
}