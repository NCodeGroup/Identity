using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class RetireClientJwksSetting : Migration
    {
        // Every table that stores a settings document (ADR-0139).
        private static readonly string[] SettingsTables =
        [
            "Servers", "Tenants", "Clients", "ResourceServers",
        ];

        // Retired setting (ADR-0161): the JSON Web Key Set a client registered by value.
        private const string RetiredSetting = "jwks";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A retired setting fails closed when a document that carries it is loaded (ADR-0139). A client's public keys
            // are its stored secrets, so the setting has no meaning to translate: dynamic client registration imported
            // the keys of every 'jwks' it stored as the client's secrets, and the registration response projects them
            // back, while a 'jwks' written any other way was never read and never authenticated anything. Removing it
            // from the document therefore changes no key a client is verified or encrypted with. A rewritten document
            // gets a new settings concurrency token, as any other settings write does.
            foreach (var table in SettingsTables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}"
                    SET "SettingsJson" = ("SettingsJson"::jsonb - '{{RetiredSetting}}')::text,
                        "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                    WHERE jsonb_typeof("SettingsJson"::jsonb) = 'object'
                        AND ("SettingsJson"::jsonb - '{{RetiredSetting}}') <> "SettingsJson"::jsonb;
                    """
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: the previous model accepts every rewritten document, and the removed setting was never
            // the source of a client's keys.
        }
    }
}
