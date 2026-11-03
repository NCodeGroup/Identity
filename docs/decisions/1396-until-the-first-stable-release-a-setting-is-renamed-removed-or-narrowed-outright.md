# 1396. Until the first stable release, a setting is renamed, removed or narrowed outright

- **Status:** Accepted (supersedes
  [ADR-1251](1251-a-retired-setting-stays-registered-until-a-later-release-removes-it.md); amends
  [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md),
  [ADR-1195](1195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md) and the retired-setting consequence of
  [ADR-1250](1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md))
- **Date:** 2026-11-03
- **Deciders:** NCode Group

## Context

Setting names are a closed vocabulary: every setting has a registered `SettingDescriptor`, and configuration, a
management write and a stored settings document that name a setting without one, or carry a value outside its domain,
are refused, the stored document by failing closed
([ADR-1139](1139-setting-names-are-a-closed-vocabulary.md),
[ADR-1140](1140-setting-values-are-validated-against-their-domain.md),
[ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)).

A released product changes its setting vocabulary across a rolling deploy: the previous release serves while the next
one rolls out and after a rollback, and it fails closed on a name it does not know
([ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). Retiring or renaming a
released setting therefore needs a way to keep the name loadable for one release, a copy of its value, and a later
contract step, and a rename needs a name reserved a release ahead. Each part is shaped by what the first release ships.

Nothing is released ([ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)). No release serves beside another,
and no stored document exists outside a developer's database, whose migration creates the schema and writes no data
([ADR-1394](1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md)). A
retired-setting state in the descriptor, and a validator that threads the stored document through every management
settings update to accept it, would carry a design with no instance in every `ISettingSerializer` implementation.

## Decision

1. **A setting is renamed, removed or narrowed outright.** Renaming a setting, removing it, narrowing the scopes it
   applies to or narrowing its domain edits or deletes its descriptor, and nothing else: no descriptor stays
   registered for a name nothing reads, and no stored document is carried forward.
2. **A stored document that names an unregistered setting fails closed.** Reading it fails as
   [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) decides, a management request that loads it answers `409`
   as [ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md) decides, and
   the configuration reader reports the name as unregistered. A developer's database that holds such a document is
   reset with `build/dev-db-reset.ps1`.
3. **Every management settings write validates the written document alone.** The server, tenant, client and resource
   server settings updates call `ISettingSerializer.ValidateSettings` with the document they store, as the create paths
   do; `ISettingSerializer` declares `DeserializeSettings` and `ValidateSettings`, and a `SettingDescriptor` has no
   retired state.
4. **A tenant's mTLS endpoint alias is only its tenant property.** `PersistedTenant.MtlsEndpointAliasAuthority` holds
   it, and no setting names it
   ([ADR-1195](1195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md)).
5. **The first stable release designs retirement.** At that release
   ([#535](https://github.com/NCodeGroup/Identity/issues/535)) the family decides how a released setting retires or is
   renamed across a rolling deploy, under the expand-and-contract rule of
   [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md), with the design of
   [ADR-1251](1251-a-retired-setting-stays-registered-until-a-later-release-removes-it.md) as prior art.

## Options considered

- **Keep the retired-setting mechanism with no instance.** Rejected: every `ISettingSerializer` implementation carries
  a member that threads the stored document through for nothing, and the mechanism settles half of a design (a
  retirement, but not a rename) that the first release has to settle whole.
- **Keep a retired descriptor for each removed pre-release setting.** Rejected: the descriptor serves documents that
  only development databases hold, and the closed vocabulary already fails them closed.
- **Remove or rename the setting outright, and design retirement with the first release (chosen).** The vocabulary is
  the set of settings the server reads, and the fail-closed read is the single path for every other name.

## Consequences

- A setting vocabulary change touches the descriptor, the code that reads the setting, and nothing under the host's
  `Migrations/` folder.
- A stored development document that still names a removed setting answers `409` from the management API and fails the
  request that loads it, as any unregistered name does, until the developer resets the database or removes the key
  through the management API.
- `ISettingSerializer` has one validation member, and a management update needs no stored document to validate.
- [#535](https://github.com/NCodeGroup/Identity/issues/535) retires this record when it decides how a released setting
  retires or is renamed.

## References

- [ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md) — the pre-release posture.
- [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) — setting names are a closed vocabulary.
- [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md) — migrations keep the
  previous release working.
- [ADR-1195](1195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md) — a tenant's mTLS endpoint alias is a
  routed tenant property.
- [ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md) — a management
  request answers `409` for a stored settings document it cannot honor.
- [ADR-1251](1251-a-retired-setting-stays-registered-until-a-later-release-removes-it.md) — the superseded
  retired-setting design.
- [ADR-1394](1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md) — one
  initial migration per host database context.
- `src/NCode.Identity.Settings.Abstractions/ISettingSerializer.cs`,
  `src/NCode.Identity/Settings/DefaultSettingSerializer.cs` and
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingConfigurationReader.cs`.
