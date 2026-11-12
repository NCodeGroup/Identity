# 1205. Replicas announce changes to each other through a best-effort broadcast port

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0140](project-template/0140-replicas-converge-through-reconciliation.md)

## Context

A tenant is materialized once and then served from `IOpenIdTenantCache`. Every management write to a tenant (its
record, its settings, a secret or its deletion) sends a `TenantChangedCommand` after it commits, and the default handler
evicts the tenant from the cache of the replica that handled the write
([ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md)).

Any replica may serve the next request
([ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md)), and an eviction on
one replica does not reach the others. A replica that keeps a cached tenant would:

- keep serving a tenant after it was disabled, deleted or given a new issuer;
- advertise an algorithm that the tenant's settings began to permit before it published the key, because the replica
  that seeded the key and the replica that served the request are not the same.

A tenant cannot be shared between replicas to avoid this: a materialized tenant holds live providers and keys and is not
serializable. The need is narrower: a replica that changes shared state tells the others, so each drops what it cached.
The replicas of a deployment already share a family of ports, with an in-process default and a Valkey adapter, for state
that every replica must see
([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)).
A change notice is a best-effort hint carrying an identifier, as project-template ADR-0140 decides; the absolute
lifetime of a cached tenant is
[ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)'s.

## Decision

1. **A replica announces a message through a port.** `NCode.Identity.SharedState.Abstractions` defines two interfaces in
   the namespace `NCode.Identity.SharedState`:
   - `IReplicaBroadcaster.PublishAsync(channel, message, cancellationToken)` announces a string message on a named
     channel.
   - `IReplicaBroadcastHandler` has a `Channel`, `HandleAsync(message, cancellationToken)` and
     `HandleMissedMessagesAsync(cancellationToken)`. A handler is registered with `TryAddEnumerable`, and several
     handlers may share a channel.

   Channels are an open vocabulary: a name compared ordinally. A feature names its own channel and registers a handler
   for it, and the broadcaster knows neither
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
2. **The semantics are those of a cache-coherence hint.** Every replica's handlers for the channel receive the message,
   the publisher's replica included, so a feature handles its own change and another replica's in one place. Delivery is
   best effort, at most once and unordered, so a handler tolerates a message that is lost, repeated or late, which means
   it is idempotent. A handler's failure is logged (`ReplicaBroadcastHandlerFailed`), the remaining handlers still run,
   and the failure never reaches the publisher.
3. **The in-process adapter is the default.** `DefaultInProcessReplicaBroadcaster` delivers a message synchronously to
   the handlers of this process that are registered for the channel. It is correct for one replica, because a message
   published by one replica is not seen by another. `AddInProcessReplicaBroadcaster()` registers it with `TryAdd`, and
   `AddIdentityServer` calls it, so a host's own registration wins. It rethrows an `OperationCanceledException` only when
   the publisher's token is cancelled, and logs a handler's own timeout or any other failure.
4. **The Valkey adapter announces with `PUBLISH` and `SUBSCRIBE`.** `AddValkey` replaces the in-process adapter, as it
   does for the other shared-state ports.
   - `ValkeyReplicaBroadcaster` runs one `PUBLISH` on the literal channel `KeyPrefix + channel`, so the key prefix also
     separates deployments that share one server. The call is recorded as `broadcast.publish` in the Valkey shared-state
     metrics. An unreachable server throws to the publisher, which decides what the loss means.
   - `ValkeyReplicaBroadcastSubscriber` is a hosted service. It subscribes this replica to the channel of every
     registered handler and retries every 5 seconds while the server is unreachable
     (`ReplicaBroadcastSubscribeFailed`). It calls `HandleMissedMessagesAsync` once a channel is subscribed, because a
     message published before the subscription took effect did not reach the replica, and again on `ConnectionRestored`.
     It hands a message to its handlers on the thread pool, so a slow handler never blocks the connection's receive
     loop.
   - The connection's access-control user needs `PUBLISH` and `SUBSCRIBE` as well as the commands of the other adapters.
5. **A message carries an identifier, never data.** The channels in use are `tenant-changed` (the tenant identifier),
   `team-changed` (the team identifier), `platform-settings-changed` and `master-key-changed`
   ([ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md)).
6. **A tenant change is announced on `tenant-changed`.**
   - `TenantChangedHandler` evicts the tenant from this replica's cache, seeds it when its settings changed, and
     announces in a `finally` block, so the announcement follows a seed that failed: the change is committed either way.
   - `TenantChangeBroadcastHandler` evicts the tenant on whichever replica receives the announcement. The eviction is
     idempotent, so the second eviction on the publishing replica is harmless.
   - A publish failure is logged (`TenantChangeBroadcastFailed`) and swallowed, because the write has committed.
7. **A missed message is bounded.** The announcement shortens the window; it is not the mechanism that closes it. On a
   restored subscription the tenant and team handlers revalidate their cached entries by row token and evict only those
   whose row changed, evicting everything when revalidation fails
   ([ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md)).
   The platform handler re-reads the platform settings, and the master-key handler evicts every cached key. A cached
   tenant lives at most its cache lifetime
   ([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)),
   so a replica that missed an announcement for any other reason, such as a publisher that could not reach Valkey,
   serves the tenant for a bounded time.
8. **A feature that needs other replicas to drop what they cached** names a channel and registers an
   `IReplicaBroadcastHandler`, changing neither the port nor an adapter.

## Options considered

- **Redis Streams with consumer groups.** Rejected: delivery is at least once, but each replica needs its own consumer
  group and the streams need trimming. An eviction is idempotent and the polls remain the fallback, so a lost message
  costs a bounded delay that at-least-once delivery does not repay.
- **Keyspace notifications.** Rejected: they are server configuration that the operator must enable, and a deployment
  that did not would lose the announcements without a sign.
- **Polling a tenant version column.** Rejected: it costs every replica a query for every cached tenant, and it still
  leaves a window of one poll interval.
- **A shared tenant cache.** Rejected: a materialized tenant holds live providers and keys and is not serializable, so
  each replica must materialize it itself.
- **Chosen:** a broadcast port with an in-process default and a Valkey adapter, carrying identifiers as best-effort
  hints.

## Consequences

- A deployment that runs several replicas needs Valkey for prompt changes as well as for single-use state
  ([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)).
  Without it the in-process adapter announces within one replica, and the others converge through their polls and the
  cache lifetime.
- When Valkey is reachable, a change reaches every replica at once instead of after the polls and the entry's
  expiration.
- A Valkey outage fails no management write: the publisher logs the failure, the `broadcast.publish` metric records it
  and the replicas converge as decision 7 describes.
- The access-control user of the Valkey connection needs `PUBLISH` and `SUBSCRIBE`.
- A new cross-replica invalidation is a channel and a handler, never a change to the port.

## References

- [project-template ADR-0140](project-template/0140-replicas-converge-through-reconciliation.md) — replicas converge through reconciliation
- [ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md) — any replica serves any request
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — channels are an open vocabulary
- [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md) — the shared-state ports
- [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md) — the tenant-changed command
- [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md) — the `master-key-changed` channel
- [ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md) — the cached tenant's lifetime
- [ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md) — reconciliation of missed announcements
- `src/NCode.Identity.SharedState.Abstractions/`, `src/NCode.Identity.SharedState.Valkey/`
