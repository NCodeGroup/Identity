# 1394. Until the first stable release, each host database context has one initial migration and no migration rewrites stored data

- **Status:** Accepted (refines
  [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md) decision 5; amends
  [ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md),
  [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) decision 4,
  [ADR-1140](1140-setting-values-are-validated-against-their-domain.md),
  [ADR-1184](1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md),
  [ADR-1250](1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md)
  decision 4, and the records whose decisions order a data migration:
  [ADR-1097](1097-sender-constrained-access-tokens-dpop-and-mtls.md),
  [ADR-1104](1104-import-public-asymmetric-secret-material.md),
  [ADR-1129](1129-the-tenant-issuer-is-the-single-authoritative-origin.md),
  [ADR-1142](1142-listed-response-types-are-the-only-front-channel-token-control.md),
  [ADR-1144](1144-authorization-codes-are-bound-to-pkce-by-default.md),
  [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method.md),
  [ADR-1148](1148-security-profiles-accumulate-down-the-settings-cascade.md),
  [ADR-1149](1149-settings-declare-the-scopes-they-apply-to.md),
  [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md),
  [ADR-1161](1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md),
  [ADR-1195](1195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md),
  [ADR-1216](1216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md),
  [ADR-1220](1220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) decision 6 and
  [ADR-1265](1265-grant-payloads-are-protected-under-the-tenant-master-key.md); and the records whose schema the
  initial migration creates:
  [ADR-1076](1076-consent-persistence-and-enforcement.md),
  [ADR-1081](1081-granular-consent-and-authorized-applications.md),
  [ADR-1189](1189-a-retired-tenant-identifier-or-issuer-is-never-reissued.md),
  [ADR-1209](1209-a-replica-adds-each-usage-batch-to-the-ledger-once.md),
  [ADR-1218](1218-ended-grants-are-removed-after-a-retention-period.md),
  [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md),
  [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md),
  [ADR-1226](1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md),
  [ADR-1234](1234-a-server-settings-reconciliation-reaches-only-the-tenants-its-change-affects.md) and
  [ADR-1268](1268-a-generated-private-key-stores-its-public-key-beside-it.md))
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md)

## Context

The reference host owns its schema as EF Core migrations in `NCode.Identity.OpenId.Host.Database`, one set for
`OpenIdDbContext` and one for `DeveloperDataProtectionDbContext`
([ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md)). A migration keeps the previous release working:
it expands, a later release contracts, and a data change says which of the two it is
([ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md),
[ADR-1184](1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md),
[ADR-1250](1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md)). A
stored settings document that names a setting without a descriptor, or a value outside its domain, fails closed
([ADR-1139](1139-setting-names-are-a-closed-vocabulary.md),
[ADR-1140](1140-setting-values-are-validated-against-their-domain.md),
[ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)), so a change to
the setting vocabulary that a deployed database stores needs a data migration of the documents.

Nothing is released ([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)). No database outside a developer's
machine holds this schema, no release serves beside a new one, and no rollback returns to one. A chain of migrations
before the first release replays schema states and data shapes that only development databases ever held: each step is
code to review, every data rewrite is hand-written SQL that the model-drift gate cannot check, and the chain names
types, columns and settings that are absent from the model. The text that describes the product describes it as it is
([project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)), and a migration
chain is that history kept in code.

## Decision

1. **Each host database context has one migration, `InitialCreate`, which creates its whole model.** It holds only
   schema operations: tables, columns, keys, indexes and foreign keys on the tables it creates. `ExpandOnlyRule.Check`
   passes it with no marker, because every constraint sits on a table the same migration creates. It creates every
   table, column and index that an amended record names a migration of its own for. `InitialCreateTests` fails a
   context that holds any migration but `InitialCreate`.
2. **A model change re-scaffolds the context's `InitialCreate`.** The author deletes the context's migration, designer
   file and model snapshot from its folder, `Migrations/<Name>` as
   [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md) item 8
   names it (`Migrations/OpenId` for `OpenIdDbContext`, `Migrations/DeveloperDataProtection` for
   `DeveloperDataProtectionDbContext`), runs `dotnet ef migrations add InitialCreate` with `--context` naming the
   context, `--output-dir` naming that folder, and `--project` and `--startup-project` both naming
   `hosts/NCode.Identity.OpenId.Host.Database`, which carries the design-time factories, then strips the byte-order mark
   from the generated files. The scaffolder gives the migration a fresh timestamp identifier on each re-scaffold. The
   model-drift gate (`dotnet ef migrations has-pending-model-changes`) checks that the model and the snapshot agree.
3. **No migration rewrites stored data.** No migration carries `Sql`, `InsertData`, `UpdateData` or `DeleteData`, and
   `InitialCreateTests` fails one that does. Renaming, retiring, re-scoping or narrowing the domain of a setting edits
   its descriptor and writes no data migration; a stored document that names an unregistered setting or holds a value
   outside its domain fails closed, as [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) and
   [ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md) decide. A change
   to how a stored row is shaped or protected changes the model and the code that reads it, and nothing reads a shape
   the serving code does not write.
4. **The expand-and-contract gate stays in force.** `ExpandOnlyRule`, `[ExpandMigration]`, `[ContractMigration]` and
   `ExpandOnlyRuleTests` are the test that
   [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md) decision 6
   requires over every migration, and they judge both contexts' `InitialCreate` migrations, which pass unmarked. The
   gate is build-time code in the host's unpacked database project: it reads the migrations that exist, and it adds no
   member to a package's surface, no state to a stored row and no path to the server's runtime. The retired-setting
   state that [ADR-1396](1396-until-the-first-stable-release-a-setting-is-renamed-removed-or-narrowed-outright.md)
   removes is not: it is a member every `ISettingSerializer` implementation carries and a runtime path that reads
   stored documents only development databases hold. The host's README states the expand-and-contract rule for the
   migrations that follow the baseline.
5. **The first stable release freezes the baseline.** At that release
   ([#535](https://github.com/NCodeGroup/identity/issues/535)) each context's `InitialCreate` becomes the release
   baseline and `InitialCreateTests` retires. From then on every model change adds a migration under
   [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md),
   [ADR-1184](1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md) and
   [ADR-1250](1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md), and
   a change to the setting vocabulary ships a data migration of the stored documents, as
   [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) decision 4 decides.

This record refines [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md)
item 8 for the reference host's two contexts: a re-scaffolded migration ships with the model change it captures
(decision 7), and with no previous release the expand-first rule (decision 4) holds for every migration.

## Options considered

- **Keep a migration per model change before the first release.** Rejected: the chain carries unreleased schema states
  and data rewrites forward for databases that do not exist, and each data rewrite is SQL that no gate checks.
- **Squash the chain once, at the first stable release.** Rejected: every model change until then adds another step
  and another data rewrite to review, and the squash at the release has to be verified against all of them at once.
- **Remove the expand-and-contract gate until the first stable release.** Rejected:
  [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md) decision 6
  requires the test over every migration, the gate judges the `InitialCreate` migrations that exist, and restoring it
  at the release would put an unexercised gate into the first release.
- **Apply the model with `EnsureCreated` until the first release.** Rejected: the migration worker, the chart's
  migration Job, `ExpandOnlyRule` and the model-drift gate all run on migrations, and switching to them at the release
  would put an unexercised path into the first release.
- **One `InitialCreate` per context, re-scaffolded on every model change (chosen).** The model is the single
  description of the schema, the migration worker and the gates run as they will after the release, and nothing
  carries an unreleased state forward.

## Consequences

- A change to the EF model touches the context's `InitialCreate`, its designer file and its model snapshot, and
  nothing else under `Migrations/`.
- A re-scaffold gives `InitialCreate` a fresh identifier. Before it applies any migration, `MigrationRunner` compares
  the identifiers each context's migrations history records with the migrations the database assembly holds; when the
  history records one the assembly lacks, it applies nothing and fails fast with a message that names the context and
  says to reset the local database with `build/dev-db-reset.ps1`, the actionable failure
  [ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md) asks for in place of a provider error.
  The check reads only the migrations history. `build/dev-db-reset.ps1` resets the local database, and the next
  AppHost run creates the schema and seeds the development data.
- A setting vocabulary change is a descriptor change: the closed vocabulary and the fail-closed read of a stored
  document stay, and no SQL accompanies the change.
- Until the first stable release, no migration carries `[ExpandMigration]` or `[ContractMigration]`: the
  `InitialCreate` migrations change no data and contract nothing. `ExpandOnlyRuleTests` exercises the markers on
  synthetic migrations, and judges the real ones.
- [#535](https://github.com/NCodeGroup/identity/issues/535) retires this record when it freezes the baseline.

## References

- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md) — the pre-release posture.
- [project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md) — the pre-release
  product is described as it is.
- [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md),
  [ADR-1184](1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md) and
  [ADR-1250](1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md) —
  the expand-and-contract rule.
- [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md).
- `hosts/NCode.Identity.OpenId.Host.Database/Migrations/OpenId/`,
  `hosts/NCode.Identity.OpenId.Host.Database/Migrations/DeveloperDataProtection/`,
  `hosts/NCode.Identity.OpenId.Host.Database/ExpandOnlyRule.cs`,
  `hosts/NCode.Identity.OpenId.Host.Database/MigrationRunner.cs`,
  `tests/NCode.Identity.OpenId.Host.Database.Tests/ExpandOnlyRuleTests.cs`,
  `tests/NCode.Identity.OpenId.Host.Database.Tests/InitialCreateTests.cs` and
  `tests/NCode.Identity.OpenId.Host.Database.Tests/MigrationRunnerTests.cs`.
- [`build/dev-db-reset.ps1`](../../build/dev-db-reset.ps1).
