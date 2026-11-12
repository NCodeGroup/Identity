# 1412. A settings change reaches every replica at once and is reconciled per team against what changed

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0140](project-template/0140-replicas-converge-through-reconciliation.md)

## Context

A tenant's effective settings come from a cascade of host configuration, the platform, the team, the tenant and the
client or resource server
([ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)). A change to a setting
that a default handler acts on, such as the algorithms a tenant's keys use or the rules a client must satisfy, has to
reach every replica at once and then reach the keys and clients that the change affects, once.

The settings that bind a tenant are those of its team, so a team is the smallest unit whose tenants share one effective
settings document above their own. A replica that serves requests keeps the platform, team and tenant in caches, so a
change must clear them everywhere. The reconciliation has to record what it last reconciled against, take a lease so
one replica does the work, and read only the tenants a change affects. A broadcast message can be lost, so it is a hint
and never the document.

## Decision

**A committed settings change is announced to every replica, and a team's tenants and clients are reconciled against
the team's effective settings, only where the change reaches them.** This record names the platform and team channels,
the cache lifetime and the per-team reconciliation pass that refine
[project-template ADR-0140](project-template/0140-replicas-converge-through-reconciliation.md).

1. **A platform write is applied and announced on its own token.** A committed platform settings write sends
   `PlatformSettingsChangedCommand`. `PlatformSettingsChangedHandler` re-reads the committed document on this replica
   before the response returns and announces `platform` on the `platform-settings-changed` channel. Every replica's
   `PlatformSettingsChangeBroadcastHandler` refreshes on a message and on missed messages. Both run on their own
   bounded token ([ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)), and a
   failure is logged and never fails the write. The broadcast contract is
   [ADR-1205](1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md)'s.
2. **The message is a hint.** The platform's persisted settings poll on an interval and refresh on demand through
   `OpenIdPlatform.RefreshSettingsAsync`, one refresh at a time. The dependency-free `IMaterializedPlatformRegistry`
   exposes the public `IMaterializedPlatformAccessor.GetPlatformAsync`, which waits for materialization.
3. **A change evicts the caches it reaches.** A platform settings change clears the team and tenant caches. A
   `team-changed` announcement evicts the team, every team beneath it and their tenants
   ([ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md)). A replica that may have missed an announcement follows
   [ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md). A
   cached tenant has an absolute `OpenIdTenantOptions.TenantCacheLifetime`, 15 minutes by default, equal to the
   secrets poll.
4. **One hosted service reconciles every team.** `TeamSettingsReconciliationService` materializes the platform at
   start, and logs `PlatformMaterializationAtStartFailed` and waits when that fails. It reconciles every team at start
   and on each signal of the platform's settings provider (a reload, a committed read, a changed poll, the algorithms
   or the descriptors). It reconciles one team with its subtree on `TeamChangedCommand(SettingsChanged: true)`, which
   reaches it through `ITeamSettingsReconciliationTrigger`. Requests coalesce.
5. **The team records what it was reconciled against.** `PersistedTeam.ReconciledSettingsFingerprint` and
   `PersistedTeam.ReconciledSettings` record what the team was last reconciled against; the platform row keeps none.
   The fingerprint is the base64url SHA-256 over the setting names and JSON values in name order, and an equal
   fingerprint means nothing to do.
6. **A lease serializes a pass.** A pass takes the lease `team-settings-reconciliation:{teamId}:{rowToken}:{fingerprint}`
   in `IExpiringKeySet` for `OpenId:Team:ReconciliationLeaseDuration`, 10 minutes by default. An unsettled pass retries
   after the lease or at the next signal, so an interrupted pass reruns within one lease.
7. **A command carries the previous settings.** `ReconcileTeamSettingsCommand(TeamId, Settings) { PreviousSettings }`
   carries the record read back through `ISettingSerializer.DeserializeSettings` at the team scope. `PreviousSettings`
   is `null` when the record is absent or unreadable (`ReconciledTeamSettingsUnreadable`), and every tenant and client
   of the team is then reconciled.
8. **The store guards the record.** `ITeamStore.UpdateReconciledSettingsAsync` writes the record only while the team
   row is unchanged since it was read. `ITeamStore.UpdateSettingsAsync` clears the fingerprint and keeps
   `ReconciledSettings`, unless the fingerprint was already clear (an earlier write was never reconciled), in which case
   it clears both.
9. **Handlers read only what changed.** `TenantKeyReconciliationHandler` reads keys only for a tenant that uses, under
   the current settings, a configured algorithm it did not use under the previous ones. A tenant that holds a key but
   none that `ITenantKeyPolicy.IsSatisfiedBy` accepts gets `TenantChangedCommand` with `SettingsChanged`, and a tenant
   that holds no key is left to its first materialization. `ClientCoherenceReportHandler` checks a tenant's clients
   only when `ReadsChangedSetting` holds for its previous and current effective settings, logs each incoherent client
   and then the count. A handler logs a tenant it cannot handle and continues.
10. **A pass records settings only.** Each pass reads every tenant's settings projection. An input that is not a
    setting (a profile definition, a key specification) is not recorded, so the next materialization and the
    incoherent-client report cover it.
11. **The key rollover runs per team.** The key rollover
    ([ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md)) takes one
    platform-wide lease per pass and walks the enabled teams, judging each team's tenants under that team's effective
    settings. It skips disabled teams and logs failed ones.

## Options considered

- **A durable job dispatcher.** A change would queue a job per replica, but the settings document is already durable,
  and a lost announcement is covered by the poll and the missed-message path. Rejected.
- **Waiting for the first request.** A tenant would be reconciled lazily, so a key or client that violates the
  settings goes unnoticed until traffic arrives. Rejected.
- **Remembering every fingerprint.** Storing each past fingerprint grows without bound, and only the last reconciled
  one decides whether there is work. Rejected.
- **Announcing the document.** A message that carries the settings can be lost or arrive stale; a hint that triggers a
  re-read is always current. Rejected.
- **Re-materializing on a change.** Rebuilding the tenant discards what is still valid and does not tell which tenants
  the change reaches. Rejected.
- **A platform-wide tenant walk.** One lease and one fingerprint, but every tenant is read on any change, however
  small, and a team's change waits behind the whole deployment. Rejected.
- **A reconciliation record per tenant.** Each tenant row would hold its own fingerprint, so a change of a parent
  settings document would touch every tenant row. Rejected.
- **Clearing the record on every write.** A write that is never reconciled would lose the settings the last pass
  reconciled against, so the next pass could not tell what changed. Rejected.
- **Refusing to start when materialization fails.** A transient failure would keep the replica down; it logs, waits and
  reconciles when the platform materializes. Rejected.

The chosen design announces a hint, re-reads the committed document, and reconciles each team against the record of
what it was last reconciled against, under a lease.

## Consequences

- A change to one team's settings reads that team's tenants and no others, and replicas contend per team.
- A platform settings change fires one pass per team, each under its own lease.
- A write that loses a race to a record write is answered `412`.
- The key rollover reads one team's tenants under that team's effective settings, so an enabled team's keys roll over
  whatever another team's settings document holds.
- The team row carries the two reconciliation columns and the platform row carries none.
- A handler a package registers receives the team's identifier and settings, never a deployment-wide walk.
- A smell: a pass that reads every tenant of the deployment for one team's change.

## References

- [project-template ADR-0140](project-template/0140-replicas-converge-through-reconciliation.md) — replicas converge
  through reconciliation
- [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) — the platform and the
  team
- [ADR-1205](1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md) — replicas announce changes through a broadcast
  port
- [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md) — a committed change is
  applied and announced on its own bounded token
- [ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md) —
  missed team announcements
- [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md) — a team nests beneath a parent
- [ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md) — key rollover
