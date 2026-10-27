# 219. Server settings converge on every replica through one reconciliation

- **Status:** Accepted (amended by
  [ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md): background work in another
  package waits for the server through `IMaterializedServerAccessor`; and
  by [ADR-0234](0234-a-server-settings-reconciliation-reaches-only-the-tenants-its-change-affects.md): the
  reconciliation records the settings it reconciled against and reaches only the tenants a change affects)
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

The server's effective settings are its configured settings with its committed settings document merged onto them
([ADR-0204](0204-a-scopes-effective-settings-are-resolved-by-one-service.md)), and they bound every tenant and client
below them. Three kinds of change move them: a management write to the document, a reload of
`OpenId:Server:Settings`, and a deployment whose configuration differs. A review (issues [#248](https://github.com/NCodeGroup/Identity/issues/248), [#249](https://github.com/NCodeGroup/Identity/issues/249), [#252](https://github.com/NCodeGroup/Identity/issues/252) and
[#263](https://github.com/NCodeGroup/Identity/issues/263)) found that none of them reached every replica, every tenant, and every client in a bounded time:

- A replica read the committed document through a poll every 5 minutes, the replica that made the write included, so
  a write was advertised and enforced up to 5 minutes late.
- A replica learns of a tenant change through the replica broadcast port, which delivers at most once
  ([ADR-0205](0205-replicas-announce-changes-through-a-broadcast-port.md)). A replica that was disconnected when a
  change was announced kept its cached tenant, and the cache's 4-hour sliding expiration kept a busy tenant
  indefinitely, so a disabled, deleted, or re-addressed tenant stayed in service there.
- A write that permitted an algorithm re-seeded the tenants it affected in a job on the in-process job dispatcher
  ([ADR-0169](0169-every-tenant-is-provisioned-with-persisted-keys.md), decision 10). The job compared the documents
  before and after the write, so a configuration change never started it; a restart lost the tenants it had not
  reached; and the pass held the dispatcher's only worker, ahead of every back-channel logout and CIBA delivery.
- A management write is refused while it would make a stored client incoherent
  ([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)). A configuration change is not
  a write, so a deployment or a reload that narrowed a ceiling left clients that failed at their next request, the
  bootstrap administrator among them, and nothing reported them.

## Decision

1. **A committed write reaches every replica at once.** The server PATCH sends `ServerSettingsChangedCommand` after it
   commits. Its default handler, `ServerSettingsChangedHandler`, reads the committed document again on this replica
   before the response returns, and then announces the server's identifier on the `server-settings-changed` channel
   of the replica broadcast port; every replica's `ServerSettingsChangeBroadcastHandler` reads the document again.
   - The message is a hint, never the document, so a replica always serves what the store committed. The poll remains
     the fallback for a lost message. A refresh or an announcement that fails is logged and never fails the write.
   - `OpenIdServer.RefreshSettingsAsync` reads the committed document again on demand. The server's persisted
     settings come from a data source that polls on its interval and refreshes on demand, one refresh at a time.
   - The server provider registers the server it materializes with `IMaterializedServerRegistry`, a singleton without
     dependencies, so a handler or a background service reaches the server without depending on the services that
     build it. The registry is reachable from other packages as the public `IMaterializedServerAccessor`, whose
     `GetServerAsync` waits until the replica materializes its server
     ([ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)).
2. **A replica that may have missed a message drops what the message would have invalidated.**
   `IReplicaBroadcastHandler.HandleMissedMessagesAsync` is called when messages on the handler's channel may not have
   reached this replica. The Valkey subscriber calls it for a channel's handlers once the channel is subscribed, and
   for every handler when its connection is restored. The tenant handler evicts every cached tenant
   (`IOpenIdTenantCache.ClearAsync`), and the server handler reads the committed document again. The in-process
   adapter delivers every message and never calls it.
3. **A cached tenant has an absolute lifetime.** `OpenIdTenantOptions.TenantCacheLifetime`, 15 minutes by default,
   bounds how long a replica serves a tenant it materialized, whatever announcements it missed, including one whose
   publisher could not reach Valkey, which no subscriber sees as a reconnect. It equals the interval of the tenant's
   secrets poll, so a replica that missed every announcement converges within the bound its polls already set, and
   the materialization that follows seeds the tenant again.
4. **What derives from the server's settings is reconciled, not replayed.** Each replica runs
   `ServerSettingsReconciliationService`. It waits until the replica materializes its server, at its first request,
   and then watches the server's settings provider, which signals a configuration reload, a refresh of the committed
   document, and a change of the registered algorithms or setting descriptors. On the first signal and on each later
   one it resolves the server's effective settings from the committed document and this replica's configuration, and
   computes their fingerprint: the base64url SHA-256 of each setting's name and JSON value, in name order, with a list
   of strings in ordinal order.
   - `PersistedServer.ReconciledSettingsFingerprint` records the fingerprint every tenant and client was last
     reconciled against. When it equals the replica's fingerprint, the replica has nothing to do.
   - Otherwise the replica takes a lease in `IExpiringKeySet` for `OpenIdServerOptions.ReconciliationLeaseDuration`,
     10 minutes by default, keyed by the server, the server row's concurrency token, and the fingerprint. Replicas
     that observe one change share one key, so one of them runs the pass. Every write to the server row changes its
     token, so a return to an earlier state, such as a write that restores a list or a rollback, is a new key.
   - The pass sends `ReconcileServerSettingsCommand` with the effective settings, and every registered handler runs.
   - The replica then records the fingerprint with `IServerStore.UpdateReconciledSettingsFingerprintAsync`, which
     writes that column alone and only while the server row is unchanged since the pass read it, so the record never
     overwrites a concurrent write. A settings write clears the fingerprint in its own unit of work, because the
     settings it was computed from changed.
   - A replica that found the lease held, whose pass failed, or whose record lost a race tries again when the lease
     expires or at the next signal. A pass that a restart interrupted is therefore run again, by another replica or
     by the restarted one, within one lease.
5. **The default handlers check the invariants from the current state.**
   - `TenantKeyReconciliationHandler` pages the tenants' settings documents, resolves each onto the effective
     settings, and reads the keys of each tenant whose effective lists name a configured algorithm. A tenant that
     holds a key but lacks one that satisfies (`ITenantKeyPolicy.IsSatisfiedBy`) an algorithm its lists name is sent
     a `TenantChangedCommand` with `SettingsChanged`, which evicts, seeds, and announces it. No other tenant is
     evicted. A tenant that holds no key at all is skipped: it has never been seeded, or its first materialization
     is seeding it, and that materialization seeds every key the tenant uses through the one pipeline
     ([ADR-0166](0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md)); the reconciliation
     fills only a gap in a seeded tenant, so it never seeds a tenant concurrently with its first request.
   - `ClientCoherenceReportHandler` pages every tenant's clients under the tenant's scope, checks each with
     `IClientCoherenceValidator`, logs a warning that names each incoherent client and every violation, and logs the
     count. A configuration change cannot be refused, so it is reported. The bootstrap administrator is checked as
     every stored client is; its seeder leaves an existing client as it is.
   - A handler logs a tenant it cannot handle and continues with the next one.
6. **The reconciliation is not a dispatched job.** It runs in its own hosted service, so a pass never holds the
   in-process dispatcher's worker, and no queued job carries state that a restart loses.

## Options considered

- **A durable `IJobDispatcher` adapter** (issue [#25](https://github.com/NCodeGroup/Identity/issues/25)). Rejected for this purpose: a stored job would carry the
  comparison of two documents, which a configuration change never produces, while the invariants can be checked from
  the current state. A durable adapter for deliveries stays deferred.
- **A pass at every replica's start.** Rejected: every rollout and every scale-out would walk every tenant and client
  once per replica. The recorded fingerprint makes a start that changes nothing free, and a start with a changed
  configuration still runs a pass.
- **Remembering every reconciled fingerprint in the shared state.** Rejected: a return to an earlier state would be
  skipped, while tenants and clients created in between were reconciled against the other state only.
- **A pass that compares the previous effective settings with the current ones.** Deferred (issue [#279](https://github.com/NCodeGroup/Identity/issues/279)): it needs
  the previous settings stored, not only their fingerprint, and a full pass runs only when the settings change.
- **Announcing the settings document.** Rejected: a message may be lost, repeated, or late, so it can only tell a
  replica to read the store.
- **Materializing the server again after a change.** Rejected: every materialized tenant holds the server's settings
  provider.
- **Either the eviction on a restored connection or the absolute lifetime alone.** Rejected: no subscriber sees a
  publisher that could not reach Valkey, and a lifetime alone keeps a stale tenant for its whole length after a
  reconnect that the replica did see.
- **Refusing to start while the configuration makes a stored client incoherent.** Rejected: one client would stop
  every replica and every tenant, where otherwise it fails alone, and the report names it.

## Consequences

- A server settings write takes effect on the replica that made it before the response returns, and on every other
  replica when the announcement arrives; a replica that misses it reads the document at its next poll.
- A restored Valkey connection evicts every tenant the replica cached, which costs one materialization per busy
  tenant.
- Each replica materializes a busy tenant again at least every 15 minutes, which seeds it, so a tenant that a pass did
  not reach gains its keys within that time.
- One pass per change of the server's effective settings reads every tenant's settings, the keys of every tenant that
  uses a configured algorithm, and every client's settings.
- A management write to the server row can lose a race to the record of a reconciliation; it is answered `412`
  ([ADR-0214](0214-a-management-write-that-loses-a-race-is-answered-412.md)), and a retry succeeds.
- A replica that has served no request has not materialized its server and does not reconcile.
- While a rollout runs replicas with different configurations, each configuration is reconciled when a replica that
  holds it starts or reloads, and the record names the last one.
- `NCode.Identity.OpenId.Core` registers `ServerSettingsReconciliationService` and does not reference the Dispatch
  abstractions.
- **Breaking:**
  - `OpenIdServer` gains `RefreshSettingsAsync`, `IOpenIdTenantCache` gains `ClearAsync`, `IReplicaBroadcastHandler`
    gains `HandleMissedMessagesAsync`, and `IServerStore` gains `UpdateReconciledSettingsFingerprintAsync`; a custom
    implementation implements them.
  - `OpenIdTenantOptions.TenantCacheExpiration`, a sliding expiration, is `TenantCacheLifetime`, an absolute one.
  - The `AddServerReconciledSettingsFingerprint` migration adds the `Servers.ReconciledSettingsFingerprint` column.
- This implements issues [#248](https://github.com/NCodeGroup/Identity/issues/248), [#249](https://github.com/NCodeGroup/Identity/issues/249), [#252](https://github.com/NCodeGroup/Identity/issues/252) and [#263](https://github.com/NCodeGroup/Identity/issues/263).

## References

- [ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md) — the job dispatch port the reconciliation does not
  use.
- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) — the shared state that holds
  the lease.
- [ADR-0169](0169-every-tenant-is-provisioned-with-persisted-keys.md) — tenant keys and the tenant change command.
- [ADR-0204](0204-a-scopes-effective-settings-are-resolved-by-one-service.md) — the effective settings resolver.
- [ADR-0205](0205-replicas-announce-changes-through-a-broadcast-port.md) — the replica broadcast port.
- [ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md) — client coherence rules.
- Code: `NCode.Identity.OpenId.Servers.ServerSettingsReconciliationService`, `DefaultServerSettingsReconciler`,
  `ServerSettingsChangedHandler`, `ServerSettingsChangeBroadcastHandler`, `DefaultMaterializedServerRegistry`,
  `NCode.Identity.OpenId.Settings.RefreshableSettingsCollectionDataSource`,
  `NCode.Identity.OpenId.Tenants.Keys.TenantKeyReconciliationHandler`,
  `NCode.Identity.OpenId.Authentication.Clients.ClientCoherenceReportHandler`,
  `NCode.Identity.SharedState.Valkey.ValkeyReplicaBroadcastSubscriber`.
