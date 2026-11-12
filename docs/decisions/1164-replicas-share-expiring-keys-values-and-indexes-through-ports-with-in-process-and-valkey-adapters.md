# 1164. Replicas share expiring keys, values and indexes through ports with in-process and Valkey adapters

- **Status:** Accepted
- **Date:** 2026-10-26
- **Deciders:** NCode Group

## Context

Any replica of a deployment may serve the next request
([ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md)), so some state must be
one set that every replica sees.

Three checks of the OP need a record that survives a change of replica:

- a server-issued DPoP nonce must be recognized by whichever replica receives the proof that echoes it
  ([RFC 9449](https://www.rfc-editor.org/rfc/rfc9449) §8);
- a DPoP proof must not be accepted twice (RFC 9449 §11.1);
- a client-authentication assertion (`private_key_jwt`, `client_secret_jwt`) must not be accepted twice
  ([RFC 7523](https://www.rfc-editor.org/rfc/rfc7523) §3, OpenID Connect Core §9).

The backend-for-frontend ([design](../backend-for-frontend.md)) keeps its state on the server and needs three more:

- a login transaction is taken exactly once: the callback that redeems it removes it atomically, so a replayed callback
  finds nothing;
- a session is updated by compare-and-swap, so two concurrent requests that refresh the same tokens cannot overwrite each
  other, and its expiry slides with activity;
- a session is found by the OP's `sid` or `sub` claim, so a back-channel logout can end every session it names.

The forces on the design:

- **A single-use check is an atomic add-if-absent.** `IDistributedCache` has only get and set, so a replay store built on
  it reads and then writes: two concurrent presentations of one proof or assertion could both be accepted, on one replica
  or on two that share a store. It has no atomic take and no compare-and-swap either.
- **A shared store needs keys built for it.** Request-supplied values joined with `:` are ambiguous (`("a:b", "c")` and
  `("a", "b:c")` meet) and unbounded in length, and a key that names no tenant collides across the tenants that share the
  store.
- **A library must not choose the host's services.** Registering an `IDistributedCache` from a library decides that
  service for the host and every other consumer of it.
- **A single replica is a legitimate production deployment**, and a replica cannot know how many replicas run.

## Decision

**State that every replica must see lives behind ports in `NCode.Identity.SharedState.Abstractions`, whose adapter the
composition root chooses: in-process for one replica, Valkey for several.**

1. **The packages.** The ports are in `NCode.Identity.SharedState.Abstractions` (namespace
   `NCode.Identity.SharedState`), the in-process defaults in `NCode.Identity.SharedState`, and the Valkey adapters in the
   opt-in package `NCode.Identity.SharedState.Valkey`, as `NCode.Identity.Jose.EdDsa` is opt-in. The family follows the
   job-dispatch port ([ADR-1242](1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md)): a `SharedStateLibrary` marker
   minted by `AddSharedStateServices()`, and adapters as opt-ins on its builder. The ports are `IExpiringKeySet`,
   `IExpiringValueSet`, `IExpiringIndexSet`, `IExpiringCounterSet`
   ([ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md)) and
   `IReplicaBroadcaster` ([ADR-1205](1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md)).
2. **The key set.** `IExpiringKeySet.TryAddAsync(key, expiresWhen)` atomically adds a key unless it is present and
   unexpired: of any number of concurrent adds for one key, across every replica that shares the set, at most one
   succeeds. `ContainsAsync(key)` reports a present, unexpired key. A key whose expiry is not in the future records
   nothing, so its add fails and the check that depends on it fails closed.
3. **The value set.** `IExpiringValueSet` holds `ExpiringValue(ReadOnlyMemory<byte> Value, long Version)`:
   - `TryAddAsync(key, value, expiresWhen)` stores a value only when the key is absent or expired, at version 1;
   - `GetAsync(key)` returns the live `ExpiringValue`, or `null` when the key is absent or expired;
   - `TryReplaceAsync(key, expectedVersion, value, expiresWhen)` is a compare-and-swap: it replaces the value only when
     the live version equals `expectedVersion`, sets the version to `expectedVersion + 1` and takes a new absolute
     expiry, so a session's idle window slides; an expiry that is not in the future returns `false` and leaves the value
     unchanged;
   - `TryTakeAsync(key)` atomically removes the live entry and returns it, so a login transaction is redeemed once;
   - `TryRemoveAsync` is an atomic compare-and-remove by value
     ([ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md));
   - `RemoveAsync(key)` removes an entry.

   Values are opaque bytes: the caller serializes them and encrypts what must not be readable in the store. A key that is
   removed and added again restarts at version 1, so a stale writer could in principle match a recycled version (ABA).
   The port does not guard against it, because callers never add a removed key again: every BFF key derives from a fresh
   256-bit random.
4. **The index set.** `IExpiringIndexSet` keeps a set of members per index key, each with its own expiry:
   `AddMemberAsync(key, member, expiresWhen)` adds a member and, on an existing member, sets its expiry;
   `GetMembersAsync(key)` returns the members that have not expired; `RemoveMemberAsync(key, member)` removes one;
   `RemoveAsync(key)` removes the whole index. An index holds the keys of the values it finds, such as every BFF session
   of one `sid` or `sub`.
5. **The in-process adapters** are correct for one replica. `AddInProcessExpiringKeySet()`,
   `AddInProcessExpiringValueSet` and `AddInProcessExpiringIndexSet` keep concurrent dictionaries, atomic among the
   threads of one process, treat expired entries as absent and sweep them at most once a minute. The value set copies a
   value on write, so a caller that reuses its buffer cannot change a stored entry. `AddIdentityServer()` registers the
   key set, the counter set and the broadcaster and exposes the builder as
   `IIdentityServerBuilder.SharedStateLibraryBuilder`; the OP uses neither the value nor the index set, so the BFF
   composition registers them.
6. **The Valkey adapters** come from `AddValkey(IConfiguration)`:
   - The server address and credentials come from the `ConnectionStrings:valkey` connection string, which accepts every
     StackExchange.Redis setting (TLS, an access-control user). The `Valkey` section binds `ValkeyOptions` strictly
     ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)):
     `KeyPrefix`, default `ncode-identity:`, so several deployments can share one server; braces are not allowed in it.
   - Without the connection string the call registers nothing and the in-process adapters stay. A `Valkey` section
     without it, a malformed connection string, or one that names no server stops the host at startup; the error never
     echoes the connection string, which may hold a password.
   - The registration takes the place of every in-process adapter registered before it.
   - The key set adds with `SET key 1 NX PX ttl`, which the server applies atomically, and looks up with `EXISTS`.
   - The value set stores one string: an 8-byte big-endian version followed by the value bytes. Add is `SET NX PX`, get is
     `GET`, take is `GETDEL` and remove is `DEL`. Replace and compare-remove are Lua scripts that compare the version
     prefix or the value and write or delete in one atomic step.
   - The index set keeps a sorted set per index, scored by each member's expiry in Unix milliseconds. Reads filter to
     scores after the replica's now. An add prunes expired members and extends the key's `PTTL` to at least the new
     member's time-to-live in one script, so an index lives at most as long as the longest time-to-live applied to it and
     never grows without bound.
   - Time-to-live values come from the replica's clock and are rounded up to whole milliseconds, so expiry does not
     depend on the server's clock.
   - The connection does not abort when the server is unreachable at startup; it reconnects in the background. A command
     against an unreachable server throws, so a single-use check fails closed (`server_error`), the BFF answers `503`,
     and a session that cannot be read is not treated as absent. A `valkey` health check, tagged `ready`, reports whether
     the server answers.
   - The adapters use StackExchange.Redis, a fully managed client that runs on every runtime identifier the host image
     targets (`linux-arm64` included), speaking the Redis protocol Valkey implements.
7. **The consumers record tenant-scoped, unambiguous, bounded keys.** The DPoP nonce service, the DPoP proof replay cache
   and the client-assertion replay cache record into `IExpiringKeySet` under `{purpose}:{tenant}:{digest}`: the tenant in
   braces is the key's hash tag
   ([ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md)), and the digest is the
   base64url SHA-256 of the length-prefixed UTF-8 components (the proof's key thumbprint and `jti`; the client identifier
   and `jti`; the nonce). `IDPoPProofReplayCache.TryRecordAsync` and `IClientAssertionReplayCache.TryRecordAsync` take the
   request's `OpenIdContext`, as `IDPoPNonceService` does, to scope the record by tenant. The library registers no
   `IDistributedCache`.
8. **The reference hosts and local development use the shared adapter.** The hosts call
   `SharedStateLibraryBuilder.AddValkey(configuration)`; the Aspire AppHost
   ([ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md))
   runs a Valkey container and injects its connection string, so local development runs the adapter a multi-replica
   deployment runs.
9. **The artifact that sets the replica count enforces it.** The production-safety gate does not require Valkey, because a
   replica cannot know how many replicas run and a single-replica production deployment on the in-process adapters is
   correct. The host's Helm chart, which sets the replica count, fails a `replicaCount` above 1 without
   `valkey.existingSecret`.
10. **Metrics.** Operations are recorded under the shared-state meter, `value_set.*` and `index_set.*` among them
    ([ADR-1197](1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md)).

## Options considered

- **`IDistributedCache` with a Redis provider.** Rejected: the abstraction has no atomic add, take or compare-and-swap, so
  a replay check stays a race across replicas, a login transaction could be redeemed twice and concurrent refreshes could
  overwrite each other; and a library would still choose the host's cache.
- **A Valkey implementation of each seam.** Rejected: every new shared record would need its own implementation per
  backend. A few ports keep a backend to one class per port and leave the consumers backend-neutral.
- **The Valkey adapter inside `NCode.Identity.Server`.** Rejected: every consumer of the turnkey server, single-replica
  self-hosters included, would carry a Redis client. The adapter is opt-in.
- **The Valkey GLIDE client.** Rejected: it carries a native core per runtime identifier, and the adapters need no
  Valkey-only command. The client sits behind the ports, so a later switch changes one class per port.
- **Refusing the in-process adapter in Production.** Rejected: a single-replica production deployment is legitimate, and
  the server cannot count replicas.
- **Hashing the tenant into the digest.** Rejected: the tenant in clear lets an operator find or remove one tenant's
  records; tenant identifiers are host labels
  ([ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)), and the digest at
  the end keeps the key unambiguous whatever the tenant contains.
- **A Valkey hash for a value** (the version and the value as fields). Rejected: `GETDEL` and `SET NX PX` work only on
  strings, and a hash needs a script for every operation, not just replace.
- **One key per index member** beside a plain set. Rejected: two keys per member and no single-key pruning, so expired
  members would need a separate sweep.
- **Chosen:** expiring key, value and index ports with in-process defaults and opt-in Valkey adapters.

## Consequences

- A deployment can run several replicas: a nonce issued by one is recognized by all, of concurrent presentations of one
  DPoP proof or client assertion exactly one is accepted, and the BFF's sessions and login transactions are the same on
  every replica. A deployment that runs several replicas must configure Valkey; with the in-process adapters each replica
  sees only its own state.
- A Valkey outage fails DPoP-bound requests and JWT client authentication closed and makes the replica not ready. A
  restart without persistence loses the records, reopening a replay window bounded by the proof and assertion lifetimes;
  enable append-only-file persistence if that matters ([hosting analysis](../hosting-analysis.md#valkey)).
- Index expiries follow the replica clocks that wrote and read them, so clock skew between replicas shifts a member's
  expiry by the skew.
- The OP registers neither the value nor the index adapter, so it carries no dependency on them.

## References

- [ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md), principle 4: shared
  ephemeral state lives in the shared-state port, backed by Valkey
- [ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md) and
  [ADR-1102](1102-jwt-bearer-client-authentication.md): the nonce and replay records
- [ADR-1242](1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md),
  [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md),
  [ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md),
  [ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md),
  [ADR-1197](1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md),
  [ADR-1205](1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md),
  [ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md),
  [ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)
- [RFC 9449](https://www.rfc-editor.org/rfc/rfc9449), [RFC 7523](https://www.rfc-editor.org/rfc/rfc7523)
- [Backend for frontend](../backend-for-frontend.md), [hosting analysis](../hosting-analysis.md#valkey)
- `src/NCode.Identity.SharedState.Abstractions/` (`IExpiringKeySet.cs`, `IExpiringValueSet.cs`, `ExpiringValue.cs`,
  `IExpiringIndexSet.cs`), `src/NCode.Identity.SharedState/InProcess/`,
  `src/NCode.Identity.SharedState.Valkey/` (`ValkeyExpiringValueSet.cs`, `ValkeyExpiringIndexSet.cs`)
