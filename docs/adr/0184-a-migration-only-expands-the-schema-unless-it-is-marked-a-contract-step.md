# 184. A migration only expands the schema unless it is marked a contract step

- **Status:** Accepted (amended by
  [ADR-0250](0250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md):
  data changes are classified, and a constraint added to an existing table contracts)
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The chart migrates the database before the rollout, while the previous release still serves, and a Helm rollback
returns to the previous release without reversing a migration
([ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). Every migration must
therefore keep the previous release working: a release expands the schema, and a later release contracts it once no
running version reads the old shape. A migration that drops or renames a column breaks the serving release the moment
the Job applies it.

`dotnet ef migrations add` scaffolds a destructive operation as readily as an additive one. A rename of a property
becomes a `RenameColumn`, and making a property required becomes an `AlterColumn` with `nullable: false`. A reviewer
must notice the operation in a generated file, and the Definition of Done's model-drift gate checks only that a
migration exists, not what it does.

## Decision

**A unit test reads every host migration's `Up` operations and fails on any operation that contracts the schema,
unless the migration class carries `[ContractMigration("why")]`.** The test runs in the Definition of Done's test stage,
so a contracting migration fails pre-push and in CI, when it is added.

1. **`ExpandOnlyRule` names the contracting operations.** It lives in `NCode.Identity.OpenId.Host.Database` and reads
   EF Core's `MigrationOperation` objects, not source text. It reports:
   - a dropped schema, table, column, or sequence;
   - a renamed table or column;
   - a required column added to an existing table without a default, which the previous release's inserts omit;
   - a column made required without a default, a column whose CLR type changes, and a column whose maximum length
     shrinks or becomes bounded.

   Creating tables, adding nullable or defaulted columns, widening or relaxing a column, and creating or dropping
   indexes all expand, so the rule accepts them.
2. **`ContractMigrationAttribute` marks a deliberate contract step.** Its required reason states why the step is safe:
   which release stopped reading the shape it removes. A contract step ships in a later release than the expansion it
   completes.
3. **The marker must be earned.** The test also fails a marked migration whose operations only expand, so a marker
   cannot outlive the operation that needed it, and it fails a marker with a blank reason.
4. **The test covers both host contexts.** It enumerates every `Migration` with a `[Migration]` attribute in the host
   database assembly, so `OpenIdDbContext` and `DeveloperDataProtectionDbContext` migrations are checked alike, and a
   new migration is checked with no registration step.

`ReplaceTenantDomainNameWithIssuerHost` is the only existing migration that contracts. It shipped before any release
existed, and it carries the marker with that reason.

## Options considered

- **A text scan in `build/dod.ps1`.** A script searches each migration's `Up` method for `DropColumn`,
  `RenameColumn`, and `nullable: false`. Rejected: separating `Up` from `Down` and reading arguments with regular
  expressions is fragile. Comparing a column's old and new nullability means parsing arguments that the operation
  objects already expose. A script also needs its own tests.
- **Check only migrations added since the last release.** This needs a release baseline in the repository, and no
  release exists yet. Rejected: a marker on the few deliberate contract steps is simpler, and it records each one's
  reason next to the code.
- **Flag raw SQL.** `migrationBuilder.Sql` can delete or rewrite data that the previous release reads. Rejected as a
  heuristic: SQL text cannot be classified reliably, and most data migrations in this repository are safe rewrites. A
  data change stays a review item.

## Consequences

- A contracting migration fails the Definition of Done with a message that names the migration and each contracting
  operation, and points to this rule.
- A contract step is visible in the code as an attribute with a reason, so a reviewer sees the deliberate exceptions
  without reading every migration.
- The rule cannot see raw SQL, a unique index that the previous release's writes violate, or a foreign key that its
  writes break. These stay review items under ADR-0180.
- `NCode.Identity.OpenId.Host.Database.Tests` is a new test project, and the host database project gains its first
  hand-written types outside `Migrations/`. `.editorconfig` treats everything under `Migrations/` as generated code,
  so hand-written code must stay outside that folder to keep its analyzers.

## References

- [ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md): migrations run before
  the rollout and keep the previous release working.
- [ADR-0073](0073-postgres-dev-database-and-aspire-migration-worker.md): the Postgres dev database and the migration
  worker.
- `src/NCode.Identity.OpenId.Host.Database/ExpandOnlyRule.cs`,
  `src/NCode.Identity.OpenId.Host.Database/ContractMigrationAttribute.cs`, and
  `tests/NCode.Identity.OpenId.Host.Database.Tests/ExpandOnlyRuleTests.cs`.
