# 234. A server settings reconciliation reaches only the tenants its change affects

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

The reconciliation of the server's effective settings
([ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md)) records only a fingerprint
of the settings it reconciled against. It cannot tell what a change moved, so each change reads the keys of every
tenant that uses a configured algorithm and checks every client of every tenant (issue [#279](https://github.com/NCodeGroup/Identity/issues/279)), however small the
change is.

Whether a change affects a tenant depends on the tenant's own document: its overrides and the security profiles it
selects decide which of the server's settings reach it
([ADR-0204](0204-a-scopes-effective-settings-are-resolved-by-one-service.md)). A comparison at the server cannot say
which tenants a change reaches, so the test compares the tenant's effective settings before and after the change.

## Decision

**A reconciliation compares the settings it reconciles with the settings it last reconciled against, and each default
handler reaches only the tenants and clients that the difference affects.**

1. **The settings are recorded beside the fingerprint.** `PersistedServer.ReconciledSettings`, the
   `ServerEntity.ReconciledSettingsJson` column that the `AddServerReconciledSettings` migration adds, holds the
   effective settings that every tenant and client was last reconciled against. `ServerSettingsSnapshot.Write` writes
   them as one JSON object that maps each setting's name to its value, serialized with the environment's JSON options,
   in ordinal order of the names. The fingerprint stays, as the cheap test of equality and the key of the lease.
   `IServerStore.UpdateReconciledSettingsAsync` records both and nothing else, only while the server row is unchanged
   since the pass read it, as the fingerprint alone is recorded.
2. **The previous settings travel with the command.** `DefaultServerSettingsReconciler` reads the record back with
   `ISettingSerializer.DeserializeSettings` at `OpenIdSettingScopes.Server` and `ISettingCollectionFactory.Create`, and
   sends `ReconcileServerSettingsCommand` with them as `PreviousSettings`. `null` means that every tenant and client is
   reconciled, as ADR-0219 describes. It is sent when the server has no record, and when the record cannot be read: a
   `JsonException` is logged as `ReconciledServerSettingsUnreadable`.
3. **A settings write keeps the record unless an earlier write was never reconciled.**
   `IServerStore.UpdateSettingsAsync` clears the fingerprint and keeps `ReconciledSettings`, so the next pass compares
   with the settings it last reconciled against. When the fingerprint is already clear, an earlier write was not
   reconciled: its settings were in effect, and tenants and clients were written under them, but no pass reconciled
   against them. `UpdateSettingsAsync` then clears `ReconciledSettings` too, so the next pass has nothing to compare
   with and reaches every tenant.
4. **Each handler tests each tenant against the previous settings.** A handler resolves the tenant's effective settings
   under `PreviousSettings` and under `Settings`, and compares them.
   - `TenantKeyReconciliationHandler` reads a tenant's keys only when the tenant uses, under `Settings`, a configured
     algorithm that it did not use under `PreviousSettings`. A tenant that used every one of them was reconciled with
     them already.
   - `ClientCoherenceReportHandler` checks a tenant's clients only when
     `IClientCoherenceValidator.ReadsChangedSetting` is `true` for the tenant's previous and current effective
     settings: a coherence rule reads a setting whose effective value differs.
5. **Every tenant's settings projection is still read.** A change affects a tenant only through the tenant's own
   document, which is known only by reading it, so both handlers page every tenant's settings. A pass saves the reads
   of keys and clients, not the read of each tenant's settings.

## Options considered

- **The full pass of ADR-0219.** Rejected: its cost grows with every tenant's keys and every client, for a change that
  affects a few tenants.
- **Compare at the server and walk the tenants that do not override a setting the change moves.** Rejected: profiles
  and merges make the tenant's effective value the only reliable test of whether a change reaches the tenant.
- **A fingerprint per tenant.** Rejected: it costs a write per tenant for each change.
- **Clear the record on every write.** Rejected: the most common change, a write, would reach every tenant.
- **Remember every intermediate state.** Rejected: the states are unbounded, and a configuration reload never reaches
  the store, so no write can record it.

## Consequences

- A change that affects few tenants reads the keys and the clients of few tenants. One pass still reads every tenant's
  settings.
- A configuration reload that is reverted before its pass records it is invisible to the record, as it is to the
  fingerprint. A tenant created while it was in effect gains a missing key when it is next materialized, within
  `TenantCacheLifetime`, and the management API lists an incoherent client
  ([ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md)).
- A tenant that a handler logs and skips during a pass, because it cannot be handled, is reached by a later pass only
  when that pass's change affects it too. A key it lacks is seeded when it is next materialized, within
  `TenantCacheLifetime`, and the management API lists its incoherent clients
  ([ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md)).
- An input of a handler's test that is not a setting, such as a security profile's definition or a key-provisioning
  specification, is not in the record: both sides of the comparison use it as it is, so a pass reaches a tenant only
  when the tenant's effective settings differ. A key that such a change requires is seeded when the tenant is next
  materialized, and the management API lists a client that it makes incoherent.
- The record adds a few kilobytes to the server row.
- **Breaking:**
  - `IServerStore.UpdateReconciledSettingsFingerprintAsync` is `UpdateReconciledSettingsAsync`, which records the
    fingerprint and the settings; a custom store implements it.
  - `PersistedServer.ReconciledSettings` and `ServerEntity.ReconciledSettingsJson` hold the settings, and the
    `AddServerReconciledSettings` migration adds the `Servers.ReconciledSettingsJson` column.
  - `ReconcileServerSettingsCommand` carries `PreviousSettings`.
- This implements issue [#279](https://github.com/NCodeGroup/Identity/issues/279).

## References

- [ADR-0169](0169-every-tenant-is-provisioned-with-persisted-keys.md) — the keys a tenant holds and the command that
  seeds them.
- [ADR-0204](0204-a-scopes-effective-settings-are-resolved-by-one-service.md) — the effective settings a tenant
  resolves.
- [ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md) — the coherence rules a change can
  affect.
- [ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md) — the reconciliation that
  this decision narrows.
- [ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md) — the report of the clients a change makes
  incoherent.
- Code: `NCode.Identity.OpenId.Servers.DefaultServerSettingsReconciler`,
  `NCode.Identity.OpenId.Servers.ServerSettingsSnapshot`,
  `NCode.Identity.OpenId.Tenants.Keys.TenantKeyReconciliationHandler`,
  `NCode.Identity.OpenId.Authentication.Clients.ClientCoherenceReportHandler`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores.ServerStore`.
