using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    [ExpandMigration(
        "Each rewritten list only drops 'plain', which this release refuses too; the previous release reads it."
    )]
    public partial class RetirePlainCodeChallengeMethod : Migration
    {
        // Every table that stores a settings document (ADR-0140: narrowing a setting's domain makes a stored document
        // that uses the removed value fail closed when it is read, so the narrowing rewrites the stored documents).
        private static readonly string[] SettingsTables = ["Servers", "Tenants", "Clients", "ResourceServers"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The PKCE 'plain' method is no longer a value of 'code_challenge_methods_supported': it exposes the code
            // verifier in the authorization request, and the domain is the registered transformations, which do not
            // include it (ADR-0144). Each list that names it is rewritten without it. The list merges by intersection,
            // so the document restricts at least what it restricted before; a list that named only 'plain' becomes
            // empty, which offers no method — the server cannot honor the one it listed.
            //
            // A document whose value for the setting is not an array already fails to load; it is left as is, rather
            // than guessing at its intent. A rewritten document gets a new settings concurrency token, as any other
            // settings write does.
            foreach (var table in SettingsTables)
            {
                migrationBuilder.Sql(
                    $$"""
                    UPDATE "{{table}}" AS target
                    SET "SettingsJson" = jsonb_set(
                            source.document,
                            '{code_challenge_methods_supported}',
                            COALESCE(
                                (
                                    SELECT jsonb_agg(element.value ORDER BY element.ordinality)
                                    FROM jsonb_array_elements(source.document -> 'code_challenge_methods_supported')
                                        WITH ORDINALITY AS element(value, ordinality)
                                    WHERE element.value <> '"plain"'::jsonb
                                ),
                                '[]'::jsonb
                            )
                        )::text,
                        "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                    FROM (
                        SELECT "Id", "SettingsJson"::jsonb AS document
                        FROM "{{table}}"
                        WHERE strpos("SettingsJson", 'code_challenge_methods_supported') > 0
                    ) AS source
                    WHERE target."Id" = source."Id"
                        AND jsonb_typeof(source.document -> 'code_challenge_methods_supported') = 'array'
                        AND source.document -> 'code_challenge_methods_supported' @> '["plain"]'::jsonb;
                    """
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: a rewritten document is valid, and restricts at least as much, under the previous
            // model, which honored any subset of 'plain' and 'S256'.
        }
    }
}
