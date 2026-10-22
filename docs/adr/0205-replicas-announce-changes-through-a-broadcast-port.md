# 205. Replicas announce changes through a broadcast port

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

A tenant is materialized once and then served from `IOpenIdTenantCache`, whose 4-hour sliding expiration keeps a busy
tenant loaded indefinitely. Every management write to a tenant — its record, its settings, a secret, or its deletion —
sends a `TenantChangedCommand` after it commits, and the default handler evicts the tenant from the cache of the replica
that handled the write ([ADR-0169](0169-every-tenant-is-provisioned-with-persisted-keys.md), decision 10).

Any replica may serve the next request ([ADR-0134](0134-cloud-native-operating-principles.md)), and the eviction reached
only the replica that made the write. A review (backlog `R49`) found that every other replica kept its cached tenant. A
replica picked up a change to the tenant's settings through its poll every 5 minutes and to its secrets through its poll
every 15, and a change that those polls do not read waited for the entry to expire. So a replica:

- kept serving a tenant after it was disabled, deleted, or moved to a new issuer, until the entry expired;
- advertised an algorithm that the tenant's settings had begun to permit before it published the key, because the
  replica that seeded the key and the replica that served the request were not the same.

A tenant cannot be shared between replicas to avoid this: a materialized tenant holds live providers and keys, and is
not serializable. The need is narrower: a replica that changes shared state tells the others, so each drops what it
cached. The replicas of a deployment already share a family of ports, with an in-process default and a Valkey adapter,
for state that every replica must see
([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)).

## Decision

1. **A replica announces a message through a port.** `NCode.Identity.SharedState.Abstractions` defines two interfaces in
   the namespace `NCode.Identity.SharedState`:
   - `IReplicaBroadcaster.PublishAsync(channel, message, cancellationToken)` announces a message on a channel.
   - `IReplicaBroadcastHandler` has a `Channel` and `HandleAsync(message, cancellationToken)`. A handler is registered
     with `TryAddEnumerable`, and several handlers may share a channel.

   Channels are an open vocabulary: a name and a string message, compared ordinally. A feature names its own channel and
   registers a handler for it, and the broadcaster knows neither.
2. **The semantics are those of a cache-coherence hint.** Every replica's handlers for the channel receive the message,
   the publisher's replica included, so a feature handles its own change and another replica's in one place. Delivery is
   best effort, at most once, and unordered. A handler therefore tolerates a message that is lost, repeated, or late,
   which means it is idempotent. A handler's failure is logged, the remaining handlers still receive the message, and
   the failure never reaches the publisher.
3. **The in-process adapter is the default.** `DefaultInProcessReplicaBroadcaster` delivers a message synchronously to
   the handlers of this process that are registered for the channel, and logs a handler's failure
   (`ReplicaBroadcastHandlerFailed`). It is correct only for one replica, because a message published by one replica is
   not seen by another. `AddInProcessReplicaBroadcaster()` registers it with `TryAdd`, and `AddIdentityServer` calls it,
   so a host that registered its own `IReplicaBroadcaster` first keeps it.
4. **The Valkey adapter announces with `PUBLISH` and `SUBSCRIBE`.** `AddValkey` replaces the in-process adapter, as it
   does for the other shared-state ports.
   - `ValkeyReplicaBroadcaster` publishes the message with one `PUBLISH` command on the literal channel
     `KeyPrefix + channel`, so the key prefix also separates deployments that share one server. A command against an
     unreachable server throws, and the publisher decides what the loss means. The call is recorded under the
     `broadcast.publish` operation of the Valkey shared-state metrics.
   - `ValkeyReplicaBroadcastSubscriber` is a hosted service. At startup it subscribes this replica to the channel of
     every registered handler, and it retries every 5 seconds while the server is unreachable
     (`ReplicaBroadcastSubscribeFailed`). The connection restores a subscription after a reconnect. A message is
     handed to its handlers on a thread-pool thread, so a slow handler never blocks the connection's receive loop, and a
     handler's failure is logged (`ReplicaBroadcastHandlerFailed`).
   - The access-control user of the connection needs the `PUBLISH` and `SUBSCRIBE` commands as well as those of the
     other adapters.
5. **A tenant change is announced on the `tenant-changed` channel.** The message is the tenant identifier.
   - `TenantChangedHandler` evicts the tenant from this replica's cache, seeds it when its settings changed, and then
     publishes the announcement in a `finally` block, so the announcement follows a seed that failed: the change is
     committed either way.
   - `TenantChangeBroadcastHandler` evicts the tenant from the cache of whichever replica receives the announcement.
     The eviction is idempotent, so the second eviction on the replica that made the change is harmless.
   - A publish failure is logged (`TenantChangeBroadcastFailed`) and swallowed. The write is already committed, so it
     succeeds, and a replica that missed the announcement converges as decision 6 describes.
6. **A missed message falls back to the polls and the cache expiration.** The announcement is a hint that shortens the
   window, not the mechanism that closes it. A replica that was disconnected when a message was published learns of a
   settings change through its poll within 5 minutes and of a secret change within 15, and drops a tenant that was
   disabled or deleted when its entry expires. Closing that remaining window, with an absolute lifetime for the cached
   tenant or a flush when the subscription reconnects, is backlog `R58`.

## Options considered

- **Redis Streams with consumer groups.** Rejected: delivery is at least once, but each replica needs its own consumer
  group, and the streams need trimming. An eviction is idempotent and the polls remain the fallback, so a lost message
  costs a bounded delay, and at-least-once delivery does not repay that machinery.
- **Keyspace notifications.** Rejected: they are server configuration that the operator must enable, and a deployment
  that did not would lose the announcements without a sign.
- **Polling a tenant version column.** Rejected: it is the cost of the polls this decision replaces, paid on every
  replica for every cached tenant, and it still leaves a window of one poll interval.
- **A shared tenant cache.** Rejected: a materialized tenant holds live providers and keys and is not serializable, so
  each replica must materialize it itself.

## Consequences

- A deployment that runs several replicas needs Valkey for prompt tenant changes as well as for single-use state
  ([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)). Without it, the in-process
  adapter announces within one replica, and the others converge through their polls.
- When Valkey is reachable, a tenant change reaches every replica at once instead of after the polls and the entry's
  expiration. A server settings change that permits an algorithm re-seeds each affected tenant through a
  `TenantChangedCommand`, so those tenants are announced too.
- A Valkey outage fails no management write. The publisher logs the failure and the `broadcast.publish` metric records
  it, and the replicas converge as decision 6 describes.
- The access-control user of the Valkey connection needs `PUBLISH` and `SUBSCRIBE`.
- A feature that needs another replica to drop what it cached names a channel and registers an
  `IReplicaBroadcastHandler`, and changes neither the port nor an adapter.
- This implements backlog `R49`.
