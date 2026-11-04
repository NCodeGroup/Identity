# 1404. Settings are reconciled per team

- **Status:** Accepted (supersedes decisions 4 and 5 of
  [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md) and amends
  [ADR-1234](1234-a-server-settings-reconciliation-reaches-only-the-tenants-its-change-affects.md),
  [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md),
  [ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md) and
  [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md))
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Refines:** [ADR-1403](1403-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)

## Context

A tenant's effective settings come from a cascade of host configuration, the platform, the team, the tenant and the
client or resource server
([ADR-1403](1403-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)). A change to a
setting that a default handler acts on, such as the algorithms a tenant's keys use or the rules a client must satisfy,
has to reach the keys and clients that the change affects, on every replica, once
([ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md)).

The settings that bind a tenant are those of its team, so a team is the smallest unit whose tenants share one
effective settings document above their own. The reconciliation has to record what it last reconciled against, take a
lease so one replica does the work, and reach only the tenants a change affects
([ADR-1234](1234-a-server-settings-reconciliation-reaches-only-the-tenants-its-change-affects.md)).

## Decision

**A team's tenants and clients are reconciled against the team's effective settings, and the team row records what they
were last reconciled against.**

1. **The team records its reconciled settings.** `PersistedTeam.ReconciledSettingsFingerprint` and
   `PersistedTeam.ReconciledSettings` hold the team's effective settings that its tenants and clients were last
   reconciled against, as ADR-1234 records them: a fingerprint, the cheap test of equality and the key of the lease,
   beside the settings as one JSON object. The platform row keeps no fingerprint: its effective settings reach tenants
   only through the teams.
2. **One service reconciles every team.** `TeamSettingsReconciliationService` reconciles every team when the
   platform's settings change token fires, and one team when `TeamChangedCommand(SettingsChanged: true)` reaches it
   through `ITeamSettingsReconciliationTrigger`.
3. **A lease serializes a pass.** A pass takes the lease `team-settings-reconciliation:{teamId}:{rowToken}:{fingerprint}`
   for `OpenId:Team:ReconciliationLeaseDuration`, so one replica reconciles a team's settings once and a pass that
   read a stale row cannot hold the lease of a newer one.
4. **A command carries the previous settings.** `ReconcileTeamSettingsCommand(TeamId, Settings) { PreviousSettings }`
   is handled by `TenantKeyReconciliationHandler` and `ClientCoherenceReportHandler`, which page only the team's
   tenants. `PreviousSettings` is `null` when the team has no record or its record cannot be read, and every tenant and
   client of the team is then reconciled.
5. **A settings write keeps the record under ADR-1234's rule.** `ITeamStore.UpdateSettingsAsync` clears
   `ReconciledSettingsFingerprint` on every write and keeps `ReconciledSettings`, unless the fingerprint was already
   clear (an earlier write was never reconciled), in which case it clears `ReconciledSettings` too, as ADR-1234
   records.
6. **Two broadcast channels announce a change.** `platform-settings-changed` carries the message `platform`, and
   `team-changed` carries the team's identifier
   ([ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md)). A committed change is applied and
   announced on its own bounded token
   ([ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md)).
7. **A change evicts the caches it reaches.** A platform settings change clears the team and tenant caches. A team
   change evicts the team and its tenants.
8. **A tenant is provisioned against its team.** A new tenant's keys are provisioned with the tenant, as
   [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md) records, and are reconciled with the team's
   settings on the team's next pass.

## Options considered

- **Platform-level reconciliation that walks every tenant per pass.** One lease and one fingerprint, but every tenant is
  read on any change, however small, and a team's change waits behind the whole deployment. Rejected.
- **Per-tenant reconciliation records.** Each tenant row would hold its own fingerprint, so a change of a parent
  settings document would touch every tenant row. Rejected.

## Consequences

- A change to one team's settings reads that team's tenants and no others, and replicas contend per team.
- A platform settings change fires one pass per team, each under its own lease.
- The team row carries the two reconciliation columns and the platform row carries none.
- A handler a package registers receives the team's identifier and settings, never a deployment-wide walk.

## References

- [ADR-1403](1403-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) — the platform and the
  team
- [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md) — settings converge through
  one reconciliation
- [ADR-1234](1234-a-server-settings-reconciliation-reaches-only-the-tenants-its-change-affects.md) — a reconciliation
  reaches only the tenants its change affects
- [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md) — replicas announce changes through a broadcast
  port
- [ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md) — a committed change is
  applied and announced on its own bounded token
- [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md) — every tenant is provisioned with persisted keys
