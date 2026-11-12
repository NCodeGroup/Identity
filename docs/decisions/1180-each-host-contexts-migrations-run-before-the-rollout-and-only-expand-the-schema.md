# 1180. Each host context's migrations run before the rollout and only expand the schema

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md)

## Context

The host never migrates on boot outside Development
([ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md)).
A schema change is a deliberate step, and replicas that start at once must not race to apply it. Locally, the Aspire
AppHost runs the migration worker `NCode.Identity.OpenId.Host.MigrationService` before the host; a Kubernetes deployment
through the host's chart
([ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)) needs the same
step. During a rolling update the previous and the next release run side by side, and a Helm rollback returns to the
previous release without running a migration, because no hook runs on rollback and EF Core cannot safely reverse every
migration. So a migration must leave the previous release working.

The host has two database contexts, the OpenId context and the DeveloperDataProtection context. Nothing is shipped, so
no database exists that a migration chain must carry forward: a model change can re-scaffold a context's single
migration instead of stacking a new one on it.

## Decision

Each host context's migrations run before the rollout, and a migration only expands the schema.
[project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md) states the
expand-and-contract rule; `ExpandOnlyRule`, the markers, `InitialCreateTests`, the `MigrationRunner` check and the chart
Job implement its items 3 to 8.

### One initial migration per context

Until the first stable release each host context has one migration, `InitialCreate`, in `Migrations/OpenId` or
`Migrations/DeveloperDataProtection` under `hosts/NCode.Identity.OpenId.Host.Database`. It holds only schema operations,
and `InitialCreateTests` fails a context that holds any other migration.

A model change re-scaffolds the migration: delete the context's migration, designer and snapshot, run

```text
dotnet ef migrations add InitialCreate --context <Context> --output-dir Migrations/<Name> --project hosts/NCode.Identity.OpenId.Host.Database --startup-project hosts/NCode.Identity.OpenId.Host.Database
```

and strip the byte-order mark from the files it writes.

### No migration rewrites stored data

`InitialCreateTests` fails a `Sql`, `InsertData`, `UpdateData` or `DeleteData` operation. Renaming, retiring or
narrowing a setting edits its descriptor, and a stored document that does not fit the descriptor fails closed
([ADR-1139](1139-setting-names-are-a-closed-vocabulary.md),
[ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)).

### A database ahead of the assembly stops the worker

`MigrationRunner` first compares each context's recorded migration ids with the assembly's. When the history names a
migration the assembly lacks, it applies nothing and throws, naming the context and `build/dev-db-reset.ps1`.

### The expand-only rule

`ExpandOnlyRule.Check(Migration)` judges every `[Migration]` of both contexts with no registration step;
`ExpandOnlyRuleTests.HostMigration_WhenChecked_KeepsThePreviousReleaseWorking` runs it over all of them. It reads EF Core
`MigrationOperation` objects, so it does not scan text:

- `FindContractingOperations` reports each operation that ADR-0143 item 5 lists as contracting, naming the table and the
  column.
- `FindDataChanges` reports `Sql`, `InsertData`, `UpdateData` and `DeleteData` operations.
- A table counts as existing unless a `CreateTableOperation` in the same operation list creates it, so a constraint on a
  table the migration creates is expansion, and a constraint added to an existing table contracts.

### Markers

- `[ExpandMigration("why")]` marks a data change that the previous release reads without failing.
- `[ContractMigration("why")]` marks a contracting operation, or a data change that the previous release fails on or
  loses.

A marker needs a non-blank reason, no migration carries both, and a marker must be earned: a marker on a migration that
needs none fails the test. The `InitialCreate` migrations pass unmarked, and synthetic migrations exercise the markers.

### What the rule cannot see

A reviewer checks these by hand:

- a column made nullable that the previous release maps as required;
- a store-type change that keeps the CLR type, such as `text` to `jsonb`, a collation change or a sequence restart;
- dropping a unique index that the previous release relies on.

### The migration runs in the chart as a hook Job

- The host image carries the worker at `/app/migrations`
  ([ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)), so a release always
  migrates to the schema its host reads and a separate migration image could never fall out of step.
- The chart runs the worker as a `pre-install` and `pre-upgrade` hook Job. Helm waits for it before it changes anything
  else, so a failed migration fails the release while the previous version serves and no pod is replaced.
- The Job has `backoffLimit` 1 and `activeDeadlineSeconds` 600, and its delete policy is
  `before-hook-creation,hook-succeeded`, so a failed Job stays for its logs until the next release replaces it.
- The Job is as restricted as the server: the image's non-root user, a read-only root filesystem, no capabilities and no
  service-account token. Its pod is labeled `app.kubernetes.io/component: migrations`, so neither the Service nor the
  PodDisruptionBudget selects it.
- The Job uses the namespace's `default` service account unless `serviceAccount.create` is false, because the chart's own
  account does not exist when a first install runs the hook.
- `migrations.database.existingSecret` may name a connection with schema rights; it defaults to the database Secret. A
  deployment that separates the two runs the host without the right to change its own schema.
- `migrations.enabled: false` removes the Job for a deployment whose DBA or pipeline applies a generated idempotent
  script.

### After the first stable release

At the first stable release
([#535](https://github.com/NCodeGroup/identity/issues/535)) each `InitialCreate` becomes the baseline and
`InitialCreateTests` is removed. From then on every model change adds an expand-first migration, a contract step ships a
release after its expansion, and a change to a setting vocabulary ships a data migration. The host's README states the
rule for every migration author.

## Options considered

- **A text scan in `dod.ps1`.** It misses what a migration builds at run time and flags what it only mentions in a
  comment. Rejected: the rule reads `MigrationOperation` objects.
- **A release baseline.** It gives the contexts a chain with nothing to carry forward. Rejected: a re-scaffolded
  `InitialCreate` is the whole history until the first release.
- **Classifying SQL text, or a marker per statement.** Rejected: a marker per migration with a reason is enough, and
  classifying text is guesswork.
- **Exempting a unique index on new columns.** Rejected: an index on a column the previous release does not know can
  still reject its writes.
- **A restricting foreign key from a new table.** Rejected: it constrains rows the previous release writes.
- **A chain of migrations, or a squash before the first release.** Rejected: nothing is shipped, so one migration per
  context is the schema.
- **Dropping the gate, or `EnsureCreated`, until the release.** Rejected: the gate runs from the first commit and the
  chart's Job needs migrations.
- **A separate migration image.** Rejected: it could fall out of step with the host image.
- **One initial migration per context, checked by tests, run by a hook Job before the rollout.** Chosen.

## Consequences

- An install or upgrade takes as long as its migrations, and Helm's `--timeout` must cover them. A long data migration
  belongs in code that runs after the rollout, not in the Job.
- The image carries the worker's few assemblies that the host does not already ship.
- A failed migration leaves the previous release serving, and the Job's logs stay for diagnosis.
- A developer whose local database is ahead of the assembly runs `build/dev-db-reset.ps1`; the worker never guesses.
- `InitialCreateTests`, `ExpandOnlyRuleTests` and `MigrationRunnerTests` fail a stray migration, a data change, an
  unearned marker and a history the assembly lacks.

## References

- `hosts/NCode.Identity.OpenId.Host.Database/ExpandOnlyRule.cs`, `ExpandMigrationAttribute.cs`,
  `ContractMigrationAttribute.cs`, `MigrationRunner.cs`, `Migrations/OpenId/` and
  `Migrations/DeveloperDataProtection/`
- `tests/NCode.Identity.OpenId.Host.Database.Tests/InitialCreateTests.cs`, `ExpandOnlyRuleTests.cs` and
  `MigrationRunnerTests.cs`
- `build/dev-db-reset.ps1`
- [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md)
- [ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md),
  [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md),
  [ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md),
  [ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md),
  [ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)
- [#535](https://github.com/NCodeGroup/identity/issues/535)
