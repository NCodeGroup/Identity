# 251. A retired setting stays registered until a later release removes it

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

[ADR-0139](0139-setting-names-are-a-closed-vocabulary.md) fails a stored document that names a setting without a
descriptor, so retiring a setting rewrote the stored documents in the same release.
[ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md) requires every migration to
keep the previous release working, during the rollout and after a rollback. The two collide when the value of a setting
leaves the settings documents.

The `PromoteMtlsEndpointAliasAuthority` migration as first written copied each tenant's `mtls_endpoint_alias_authority`
into the tenant's alias column and deleted the key. The previous release routes and advertises the alias from the key,
so it lost every alias during the rollout and after a rollback, and `Down` did not restore it (issue [#206](https://github.com/NCodeGroup/Identity/issues/206)).

## Decision

**A setting whose value leaves the settings documents stays registered as retired. Reading ignores it, writing refuses
it, and a later release removes it from the stored documents together with its descriptor.**

1. **A descriptor can be retired.** `SettingDescriptor.RetiredReason` states why, and `IsRetired` is true when it is
   set. A retired descriptor keeps its name, value type and scopes, and it has no default, no rule and no discovery.
2. **Reading ignores a retired setting, and a write refuses it unless it keeps the stored value.**
   `ISettingSerializer.DeserializeSettings` skips a retired setting, so a stored document that carries it loads.
   `ValidateSettings` refuses it with `The setting '{name}' is retired. {reason}`, and the configuration reader refuses
   it with `'{path}' is retired. {reason}`. `ISettingSerializer.ValidateSettingsUpdate` takes the stored document beside
   the written one and validates like `ValidateSettings`, except that it accepts a retired setting whose value equals
   the stored value (`JsonElement.DeepEquals`). Removing a retired setting is always accepted. Every management settings
   update, for the server, a tenant, a client and a resource server, validates through `ValidateSettingsUpdate` against
   the stored document, so an owner keeps or removes a retired setting but cannot add it or change its value. The create
   paths keep `ValidateSettings`.
3. **Retiring a setting is an expansion followed by a contraction.** A migration that retires a setting, or gives its
   value a home outside the settings documents, carries `[ExpandMigration]`: it copies the value and leaves the key, and
   its `Down` writes the value back. A later release's `[ContractMigration]` removes the key from every settings table,
   and the retired descriptor is deleted
   ([ADR-0250](0250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md)).
4. **`mtls_endpoint_alias_authority` is retired.** The alias is a routed tenant property
   ([ADR-0195](0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md)), and the descriptor is registered
   with a reason in `DefaultSettingDescriptorDataSource`. `PromoteMtlsEndpointAliasAuthority`, which is in no release
   and is edited in place, copies each non-empty string value into the alias columns, gives each copied tenant a fresh
   concurrency token, and leaves `SettingsJson` untouched. Its `Down` writes each tenant's alias into the key, and
   removes a stale non-empty key from a tenant that has none, before it drops the columns. The contract step that
   removes the key is tracked as issue [#37](https://github.com/NCodeGroup/Identity/issues/37).
5. **A rename stays a contract step.** The previous release fails closed on a setting name it has no descriptor for, so
   a rename cannot expand.

## Options considered

- **A corrective migration.** Rejected: it would delete and restore the key on every database built from scratch, and
  no release contains the original migration.
- **Write the retired key from the serving release.** Rejected: it puts a setting that this release does not honor into
  documents that this release validates. `Down` covers a deliberate downgrade, and a Helm rollback serves the value as
  of the migration.
- **Accept a retired setting on every write.** Rejected: a value that nothing reads is the silent failure that ADR-0139
  rules out.
- **Drop retired keys on the next write.** Rejected: the previous release loses the value of every owner that is written
  during the rollout.
- **Reserve a name one release ahead so that a rename expands.** Deferred until a release exists.

## Consequences

- The serving release reads every document the previous release wrote, and a document that carries a retired setting is
  one it can read, so it does not answer `409`
  ([ADR-0245](0245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)).
- An alias changed through the serving release reaches the previous release only through `Down`.
- An alias written through the previous release while this release rolls out, by a settings update or by a tenant
  created with the setting, reaches only the setting: this release ignores it, and `Down` replaces it with the
  tenant's alias or removes it. Set an alias through this release's management API once the rollout completes.
- A settings read of the management API shows the retired key until the contract step.
- **Breaking:** `ISettingSerializer` declares `ValidateSettingsUpdate`, which every implementation provides.

## References

- [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md): setting names are a closed vocabulary.
- [ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md): migrations run before
  the rollout and keep the previous release working.
- [ADR-0195](0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md): a tenant's mTLS endpoint alias is a
  routed tenant property.
- [ADR-0245](0245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md): a management
  request answers 409 for a stored settings document it cannot honor.
- [ADR-0250](0250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md): a
  migration classifies its data change.
- `src/NCode.Identity/Settings/DefaultSettingSerializer.cs`,
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs`, and
  `src/NCode.Identity.OpenId.Host.Database/Migrations/OpenId/20261021021124_PromoteMtlsEndpointAliasAuthority.cs`.
