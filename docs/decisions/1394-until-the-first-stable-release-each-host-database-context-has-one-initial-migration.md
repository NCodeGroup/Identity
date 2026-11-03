# 1394. Until the first stable release, each host database context has one initial migration and no migration rewrites stored data

- **Status:** Accepted (refines
  [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md) decision 5; amends
  [ADR-1041](1041-host-owned-ef-migrations-deliberate-outside-development.md),
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
  [ADR-1265](1265-grant-payloads-are-protected-under-the-tenant-master-key.md))
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md)

## Context

The reference host owns its schema as EF Core migrations in `NCode.Identity.OpenId.Host.Database`, one set for
`OpenIdDbContext` and one for `DeveloperDataProtectionDbContext`
([ADR-1041](1041-host-owned-ef-migrations-deliberate-outside-development.md),
[ADR-1073](1073-postgres-dev-database-and-aspire-migration-worker.md)). A migration keeps the previous release working:
it expands, a later release contracts, and a data change says which of the two it is
([ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md),
[ADR-1184](1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md),
[ADR-1250](1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md)). A
stored settings document that names a setting without a descriptor, or a value outside its domain, fails closed
([ADR-1139](1139-setting-names-are-a-closed-vocabulary.md),
[ADR-1140](1140-setting-values-are-validated-against-their-domain.md),
[ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)), so a change to
the setting vocabulary that a deployed database stores needs a data migration of the documents.

Nothing is released ([ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)). No database outside a developer's
machine holds this schema, no release serves beside a new one, and no rollback returns to one. A chain of migrations
before the first release replays schema states and data shapes that only development databases ever held: each step is
code to review, every data rewrite is hand-written SQL that the model-drift gate cannot check, and the chain names
types, columns and settings that are absent from the model. The text that describes the product describes it as it is
([ADR-1393](1393-the-pre-release-product-is-described-as-it-is-and-its-changes-are-not-tracked.md)), and a migration
chain is that history kept in code.

## Decision

1. **Each host database context has one migration, `InitialCreate`, which creates its whole model.** It holds only
   schema operations: tables, columns, keys, indexes and foreign keys on the tables it creates. `ExpandOnlyRule.Check`
   passes it with no marker, because every constraint sits on a table the same migration creates.
2. **A model change re-scaffolds the context's `InitialCreate`.** The author deletes the context's migration and model
   snapshot and runs `dotnet ef migrations add InitialCreate` for that context into its `Migrations/<context>` folder,
   then strips the byte-order mark from the generated files. The scaffolder gives the migration a fresh timestamp
   identifier on each re-scaffold. The model-drift gate (`dotnet ef migrations has-pending-model-changes`) checks that
   the model and the snapshot agree.
3. **No migration rewrites stored data.** No migration carries `Sql`, `InsertData`, `UpdateData` or `DeleteData`.
   Renaming, retiring, re-scoping or narrowing the domain of a setting edits its descriptor and writes no data
   migration; a stored document that names an unregistered setting or holds a value outside its domain fails closed,
   as [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) and
   [ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md) decide. A
   change to how a stored row is shaped or protected changes the model and the code that reads it, and nothing reads a
   shape the serving code does not write.
4. **The expand-and-contract machinery stays in force.** `ExpandOnlyRule`, `[ExpandMigration]`, `[ContractMigration]`
   and `ExpandOnlyRuleTests` check every migration of both contexts, the `InitialCreate` migrations included, and the
   host's README keeps the rule for every migration after the first stable release.
5. **The first stable release freezes the baseline.** At that release
   ([#535](https://github.com/NCodeGroup/Identity/issues/535)) each context's `InitialCreate` becomes the release
   baseline. From then on every model change adds a migration under
   [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md),
   [ADR-1184](1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md) and
   [ADR-1250](1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md), and
   a change to the setting vocabulary ships a data migration of the stored documents, as
   [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) decision 4 decides.

This record refines [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md)
without departing from it: a re-scaffolded migration ships with the model change it captures (decision 7), and with no
previous release the expand-first rule (decision 4) holds for every migration.

## Options considered

- **Keep a migration per model change before the first release.** Rejected: the chain carries unreleased schema states
  and data rewrites forward for databases that do not exist, and each data rewrite is SQL that no gate checks.
- **Squash the chain once, at the first stable release.** Rejected: every model change until then adds another step
  and another data rewrite to review, and the squash at the release has to be verified against all of them at once.
- **Apply the model with `EnsureCreated` until the first release.** Rejected: the migration worker, the chart's
  migration Job, `ExpandOnlyRule` and the model-drift gate all run on migrations, and switching to them at the release
  would put an unexercised path into the first release.
- **One `InitialCreate` per context, re-scaffolded on every model change (chosen).** The model is the single
  description of the schema, the migration worker and the gates run as they will after the release, and nothing
  carries an unreleased state forward.

## Consequences

- A change to the EF model touches the context's `InitialCreate`, its designer file and its model snapshot, and
  nothing else under `Migrations/`.
- A re-scaffold changes the migration's identifier, so a development database built from an earlier `InitialCreate`
  fails on its first `CREATE TABLE` when the migration worker applies the new one. `build/dev-db-reset.ps1` resets it,
  and the next AppHost run creates the schema and seeds the development data.
- A setting vocabulary change is a descriptor change: the closed vocabulary and the fail-closed read of a stored
  document stay, and no SQL accompanies the change.
- Until the first stable release, the `[ExpandMigration]` and `[ContractMigration]` markers have no instance. The rule
  and its synthetic tests keep them working for the first migration that follows the baseline.
- [#535](https://github.com/NCodeGroup/Identity/issues/535) retires this record when it freezes the baseline.

## References

- [ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md) — the pre-release posture.
- [ADR-1393](1393-the-pre-release-product-is-described-as-it-is-and-its-changes-are-not-tracked.md) — the pre-release
  product is described as it is.
- [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md),
  [ADR-1184](1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md) and
  [ADR-1250](1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md) —
  the expand-and-contract rule.
- [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md).
- `src/NCode.Identity.OpenId.Host.Database/Migrations/OpenId/`,
  `src/NCode.Identity.OpenId.Host.Database/Migrations/DataProtection/`,
  `src/NCode.Identity.OpenId.Host.Database/ExpandOnlyRule.cs` and
  `tests/NCode.Identity.OpenId.Host.Database.Tests/ExpandOnlyRuleTests.cs`.
- [`build/dev-db-reset.ps1`](../../build/dev-db-reset.ps1).
