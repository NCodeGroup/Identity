# 250. A migration classifies its data change, and a constraint added to an existing table contracts

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

[ADR-0184](0184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md) reads the schema operations
of a migration and leaves raw SQL to review (issue [#205](https://github.com/NCodeGroup/Identity/issues/205)). The rule leaves four gaps:

- It passes a column made required with a default. EF Core writes an explicit NULL for every column the previous
  release maps as nullable, so a default never protects that release's inserts, and rows holding NULL block the change.
- It passes a unique index, unique constraint, primary key, check constraint or foreign key added to an existing table.
  Each refuses writes that the previous release makes.
- It passes a foreign key whose delete rule is `Restrict` or `NoAction`. The previous release does not know the
  dependents that this release writes, so its delete of a row they reference fails.
- It cannot see data changes. Most data migrations rewrite settings documents, and a renamed setting makes the previous
  release fail closed, because a stored document that names a setting without a descriptor is refused
  ([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)).

## Decision

**`ExpandOnlyRule.Check` judges every host migration. It reports the operations that tighten a table the previous
release writes, and it requires a marker that states what a data change does to the previous release.**

1. **The rule reports more contracting operations.** `ExpandOnlyRule.FindContractingOperations` keeps the findings of
   ADR-0184 and adds:
   - a column made required, with or without a default (`makes column T.C required`);
   - a required column whose default is removed (`removes the default of required column T.C`);
   - a precision or scale that narrows (`narrows the precision or scale of column T.C`);
   - a unique index, unique constraint, primary key, check constraint or foreign key added to an existing table
     (`adds unique index N to existing table T`, `adds unique constraint N to existing table T`,
     `adds primary key N to existing table T`, `adds check constraint N to existing table T`,
     `adds foreign key N to existing table T`);
   - a foreign key declared by a table that the migration creates whose delete rule is `Restrict` or `NoAction` and
     whose principal table exists (`adds foreign key N, which restricts deleting rows of existing table P`).

   A table exists unless a `CreateTableOperation` in the same operation list creates it, in the same schema and with
   the same name. A unique index, a constraint or a foreign key on a table that the migration creates expands, because
   the previous release writes no row to it.
2. **A data change carries a marker.** `ExpandOnlyRule.FindDataChanges` reports `SqlOperation` (`runs raw SQL`),
   `InsertDataOperation` (`inserts data into T`), `UpdateDataOperation` (`updates data in T`) and
   `DeleteDataOperation` (`deletes data from T`). A migration that changes data carries one marker with a reason:
   - `[ExpandMigration("why")]` when every row the migration writes is one the previous release does not read, or reads
     without failing, with the same meaning or a restriction that this release applies too;
   - `[ContractMigration("why")]` when the previous release fails on, or loses, a value it reads: a renamed setting, a
     loosened restriction, a removed value.

   `ExpandMigrationAttribute` is shaped like `ContractMigrationAttribute`: a class marker with a required reason.
3. **`ExpandOnlyRule.Check` judges a migration.** `Check(Migration migration)` returns one sentence per violation:
   - A migration without a marker has no contracting operation and no data change.
   - An `[ExpandMigration]` migration has at least one data change and no contracting operation.
   - A `[ContractMigration]` migration has a contracting operation or a data change.
   - A marker states a reason that is not blank, and no migration carries both markers.

   `ExpandOnlyRuleTests.HostMigration_WhenChecked_KeepsThePreviousReleaseWorking` runs `Check` over every migration of
   both host contexts, so a migration is checked with no registration step, and a marker cannot outlive the change that
   needed it.
4. **Each existing migration is classified.** The classification is the reviewed judgement of what each migration does
   to the previous release:
   - Expand: `AddSecretEncoding`, `RetireAllowUnsafeTokenResponse`, `RetirePlainCodeChallengeMethod`,
     `RegisterClientAuthenticationMethod`, `RemoveSettingsOutsideTheirScopes`, `PromoteMtlsEndpointAliasAuthority` and
     `BoundEndUserSessions`.
   - Contract: `ReplaceTenantDomainNameWithIssuerHost`, `AddConsents`, `AddClientResourceServer`,
     `TranslateSecurityProfileToSecurityProfiles`, `RetireUnsafeJoseSettingValues`, `RetireClientJwksSetting`,
     `SeparateCertificateBindingRequirement`, `RenameDPoPRequirement` and `RenameRefreshTokenRotationSetting`.
   - Every other migration is unmarked.

   No release exists ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)), so the reason of each contract
   marker says that the step shipped before any release. From the first release on, a contract step ships a release
   after the expansion that it completes.

## Options considered

- **Classify raw SQL by its text.** Rejected: SQL text cannot be classified reliably, as ADR-0184 finds. A marker states
  what the SQL does, and a reviewer checks the statement against the reason.
- **One marker per SQL statement.** Rejected: the statements of a migration form one change, and a reviewer reads one
  reason on the class.
- **Exempt a unique index on columns that the same migration adds.** Rejected: a required column with a default gives
  every row of the previous release one value, so the index refuses the second insert. A contract marker states the
  reason instead.
- **Accept a restricting foreign key from a table that the migration creates.** Rejected: once this release writes a
  dependent row, the previous release's delete of the row it references fails.

## Consequences

- Every data migration carries a reason that a reviewer can check against its statements.
- A constraint on an existing table ships in two releases, or as a contract step with its reason.
- A column made required fails the Definition of Done even with a default.
- These stay review items, because the rule cannot read them: a column made nullable that this release writes NULL
  into, which the previous release maps as required and fails to read; a store-type change that keeps the CLR type
  (`text` to `jsonb`), a collation change, a sequence restart, and dropping a unique index that the previous release
  relies on.
- A migration that rewrites settings documents expands only when the previous release reads every rewritten document
  without failing, with the same meaning or a restriction that this release applies too. A renamed setting contracts,
  and a retired setting keeps its stored key until a later release removes it
  ([ADR-0251](0251-a-retired-setting-stays-registered-until-a-later-release-removes-it.md)).

## References

- [ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md): migrations run before
  the rollout and keep the previous release working.
- [ADR-0184](0184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md): a migration only expands
  the schema unless it is marked a contract step.
- [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md): setting names are a closed vocabulary.
- [ADR-0251](0251-a-retired-setting-stays-registered-until-a-later-release-removes-it.md): a retired setting stays
  registered until a later release removes it.
- `src/NCode.Identity.OpenId.Host.Database/ExpandOnlyRule.cs`,
  `src/NCode.Identity.OpenId.Host.Database/ExpandMigrationAttribute.cs`,
  `src/NCode.Identity.OpenId.Host.Database/ContractMigrationAttribute.cs`, and
  `tests/NCode.Identity.OpenId.Host.Database.Tests/ExpandOnlyRuleTests.cs`.
