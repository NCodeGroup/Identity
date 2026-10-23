using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCode.Identity.OpenId.Host.Database.Migrations.OpenId
{
    /// <inheritdoc />
    public partial class BoundEndUserSessions : Migration
    {
        // Every table that stores a settings document that can name the session settings. The settings apply to
        // tenants, and a server document may carry them as a default for its tenants.
        private static readonly string[] Tables = ["Servers", "Tenants"];

        // The session settings whose domain is bounded, each with the longest value its domain accepts. The domain of
        // a setting is between one second and that maximum.
        private static readonly (string Name, string Maximum)[] SessionSettings =
        [
            ("session_absolute_lifetime", "365 days"),
            ("session_idle_timeout", "100 days"),
        ];

        // The form in which a stored TimeSpan is written: an optional number of days of at most eight digits, hours 00
        // to 23, minutes and seconds 00 to 59, and optional fractional seconds. The bounds keep the cast to an interval
        // from failing: PostgreSQL raises 'interval field value out of range' for a day count past 2,147,483,647 and
        // for minutes or seconds of 60 or more, and no TimeSpan has such a value.
        private const string TimeSpanPattern = @"^([0-9]{1,8}\.)?([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\.[0-9]{1,7})?$";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous release keeps serving while this migration is applied (ADR-0180), and ADR-0184 leaves a
            // data rewrite to review. That release accepts every row that this migration rewrites: a document without
            // a session setting is valid, and a grant with an expiry is valid.
            //
            // A session setting is removed when its value is a TimeSpan that its domain refuses, for example zero,
            // which used to mean that the limit was off. The value only allowed a session that no domain accepts, and
            // the default applies instead.
            //
            // The CASE guards the cast: only a value in TimeSpan form is converted to an interval, and the days of
            // that form are rewritten to the interval syntax. A document that is not a JSON object, and a value that
            // is not a string in TimeSpan form, are left as is, rather than guessing at their intent. A value that has
            // the shape of a TimeSpan but not its range is left as is, like any other value that is not a TimeSpan.
            //
            // Every rewritten row gets a new concurrency token, as any other write does: a settings concurrency token
            // for a settings document, and a concurrency token for a grant.
            foreach (var table in Tables)
            {
                foreach (var (name, maximum) in SessionSettings)
                {
                    migrationBuilder.Sql(
                        $$"""
                        UPDATE "{{table}}" AS target
                        SET "SettingsJson" = (source.document - '{{name}}')::text,
                            "SettingsConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                        FROM (
                            SELECT "Id", "SettingsJson"::jsonb AS document
                            FROM "{{table}}"
                            WHERE strpos("SettingsJson", '{{name}}') > 0
                        ) AS source
                        WHERE target."Id" = source."Id"
                            AND jsonb_typeof(source.document) = 'object'
                            AND jsonb_typeof(source.document -> '{{name}}') = 'string'
                            AND CASE
                                WHEN (source.document ->> '{{name}}') ~ '{{TimeSpanPattern}}'
                                THEN regexp_replace(source.document ->> '{{name}}', '^([0-9]+)\.', '\1 days ')::interval
                                    NOT BETWEEN interval '1 second' AND interval '{{maximum}}'
                                ELSE false
                            END;
                        """
                    );
                }
            }

            // A session grant that never expires and is not revoked is a session that never ends. It ends 30 days,
            // the default absolute lifetime, after it was created, and the grant prune removes it after the retention
            // from then on.
            migrationBuilder.Sql(
                """
                UPDATE "Grants"
                SET "ExpiresWhen" = "CreatedWhen" + interval '30 days',
                    "ConcurrencyToken" = replace(gen_random_uuid()::text, '-', '')
                WHERE "GrantType" = 'session'
                    AND "ExpiresWhen" IS NULL
                    AND "RevokedWhen" IS NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: a removed session setting and a stamped expiry only bound a session that no domain
            // accepts, and the previous release serves a bounded session as it serves any other. The rewritten rows
            // cannot be told from the rows that were never rewritten.
        }
    }
}
