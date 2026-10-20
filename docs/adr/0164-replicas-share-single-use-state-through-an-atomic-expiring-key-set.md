# 164. Replicas share single-use state through an atomic expiring key set, backed by Valkey

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

Three checks need state that every replica of a deployment sees, because any replica may serve the next request
([ADR-0134](0134-cloud-native-operating-principles.md)):

- a server-issued DPoP nonce must be recognized by whichever replica receives the proof that echoes it
  ([RFC 9449](https://www.rfc-editor.org/rfc/rfc9449) §8);
- a DPoP proof must not be accepted twice (RFC 9449 §11.1);
- a client-authentication assertion (`private_key_jwt`, `client_secret_jwt`) must not be accepted twice
  ([RFC 7523](https://www.rfc-editor.org/rfc/rfc7523) §3, OpenID Connect Core §9).

[ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md) and
[ADR-0102](0102-jwt-bearer-client-authentication.md) placed all three over `IDistributedCache`, with the library
registering the in-memory provider so a single node worked unchanged and a cluster could swap in a shared one. That model
fails on four counts:

- **The abstraction cannot express the operation.** A single-use check is an atomic add-if-absent. `IDistributedCache`
  has only get and set, so the replay stores read and then wrote: two concurrent presentations of one proof or
  assertion — on one replica or on two sharing a store — could both be accepted, although both seams promise atomic
  recording.
- **The default is per replica.** Nothing in the server or the reference host registered a shared provider, so a nonce
  issued by one replica was rejected by another, and a proof or assertion replayed to another replica was accepted.
  Running more than one replica was unsafe.
- **The keys were not built for a shared store.** They joined the request-supplied values with `:` — ambiguous
  (`("a:b", "c")` and `("a", "b:c")` meet), unbounded in length — and named no tenant, so on a store every tenant shares,
  one tenant's records collided with another's.
- **A library imposed a host service.** Registering an `IDistributedCache` from a library decides that service for the
  host and every other consumer of it.

## Decision

**Single-use and freshness records go to one atomic port, `IExpiringKeySet`, whose adapter the composition root
chooses: in-process for a single replica, Valkey for several.**

1. **The port.** `NCode.Identity.SharedState.Abstractions` defines `IExpiringKeySet`: `TryAddAsync(key, expiresWhen)`
   atomically adds a key unless it is present and unexpired — of any number of concurrent adds for one key, across every
   replica that shares the set, at most one succeeds — and `ContainsAsync(key)` reports a present, unexpired key. A key
   whose expiry is not in the future is not recorded, so its add fails and the check that depends on it fails closed.
   The family follows the job-dispatch port ([ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md)): a
   `SharedStateLibrary` marker minted by `AddSharedStateServices()`, and adapters as opt-ins on its builder.

2. **The in-process adapter is the default.** `NCode.Identity.SharedState` registers `AddInProcessExpiringKeySet()`: a
   concurrent dictionary of keys to expiries, atomic among the threads of one process, with expired keys treated as
   absent and swept at most once a minute. `AddIdentityServer()` registers it and exposes the builder as
   `IIdentityServerBuilder.SharedStateLibraryBuilder`. It is correct only for a single replica.

3. **The Valkey adapter is an opt-in package.** `NCode.Identity.SharedState.Valkey` adds `AddValkey(IConfiguration)`:
   - The server address and credentials come from the `ConnectionStrings:valkey` connection string, which accepts every
     StackExchange.Redis setting (TLS, an access-control user); the `Valkey` section binds `ValkeyOptions` strictly
     ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)) — today its `KeyPrefix`, so several
     deployments can share one server.
   - Without the connection string the call registers nothing and the in-process adapter stays. A `Valkey` section
     without it, a malformed connection string, or one that names no server stops the host at startup; the error never
     echoes the connection string, which may hold a password.
   - An add is one `SET key 1 NX PX ttl` command, which the server applies atomically; the time-to-live is computed from
     the replica's clock and rounded up to whole milliseconds, so expiry does not depend on the server's clock. A lookup
     is `EXISTS`.
   - The connection does not abort when the server is unreachable at startup; it reconnects in the background. A
     command against an unreachable server throws, so a single-use check fails closed (`server_error`) rather than
     accepting a request it cannot check. A `valkey` health check, tagged `ready`, reports whether the server answers.
   - The registration replaces the adapter registered before it.

   The adapter uses StackExchange.Redis: a fully managed client that runs on every runtime identifier the host image
   targets (`linux-arm64` included), speaking the Redis protocol Valkey implements. The adapter needs only `SET NX PX`,
   `EXISTS`, and `PING`.

4. **The consumers record tenant-scoped, unambiguous, bounded keys.** The DPoP nonce service, the DPoP proof replay
   cache, and the client-assertion replay cache record into `IExpiringKeySet` under `{purpose}:{tenantId}:{digest}`,
   where the digest is the base64url SHA-256 of the length-prefixed UTF-8 components (the proof's key thumbprint and
   `jti`; the client identifier and `jti`; the nonce). `IDPoPProofReplayCache.TryRecordAsync` and
   `IClientAssertionReplayCache.TryRecordAsync` take the request's `OpenIdContext`, as `IDPoPNonceService` does, to
   scope the record by tenant. The library registers no `IDistributedCache`.

5. **The reference host and local development use the shared adapter.** The host calls
   `SharedStateLibraryBuilder.AddValkey(configuration)`; the Aspire AppHost
   ([ADR-0071](0071-aspire-local-dev-orchestrator.md)) runs a Valkey container and injects its connection string, so
   local development runs the adapter a multi-replica deployment runs.

6. **The replica count is enforced by the artifact that sets it.** A replica cannot know how many replicas run, and a
   single-replica production deployment on the in-process adapter is correct, so the production-safety gate does not
   require Valkey. The deployment artifact that sets the replica count — the host's Helm chart — requires the Valkey
   connection whenever it runs more than one replica.

## Options considered

- **Keep `IDistributedCache` and register a Redis provider.** Rejected: the abstraction has no atomic add-if-absent, so
  the replay check stays a race, now across replicas; and a library would still choose the host's cache.
- **Keep `IDistributedCache` for nonces and add an atomic primitive for replay alone.** Rejected: two stores and two
  adapters to configure for one concern, when a nonce needs only the same primitive's lookup.
- **A Valkey implementation of each replay seam.** Rejected: every new single-use record would need its own
  implementation per backend. One primitive keeps a backend to one class and leaves the seams backend-neutral.
- **Put the Valkey adapter in `NCode.Identity.Server`.** Rejected: every consumer of the turnkey server, single-replica
  self-hosters included, would carry a Redis client. The adapter is opt-in, as `NCode.Identity.Jose.EdDsa` is.
- **The Valkey GLIDE client.** Not chosen: it carries a native core per runtime identifier, and the adapter needs no
  Valkey-only command. The client sits behind the port, so a later switch changes one class.
- **Refuse the in-process adapter in Production.** Rejected: a single-replica production deployment is legitimate, and
  the server cannot count replicas (decision 6).
- **Hash the tenant into the digest.** Rejected: the tenant in clear lets an operator find or remove one tenant's
  records; tenant identifiers are host labels ([ADR-0159](0159-a-tenant-is-named-by-its-creator.md)), and the digest at
  the end keeps the key unambiguous whatever the tenant contains.

## Consequences

- A deployment can run several replicas: a nonce issued by one is recognized by all, and of concurrent presentations of
  one DPoP proof or client assertion exactly one is accepted, on one replica or many.
- A Valkey outage fails the requests that need a single-use check — DPoP-bound requests and JWT client authentication
  — closed, and the readiness check reports it. A Valkey restart without persistence loses the records, reopening a
  replay window bounded by the proof and assertion lifetimes; enable append-only-file persistence if that matters
  ([hosting analysis](../hosting-analysis.md#valkey)).
- **Breaking changes**, accepted under the pre-release posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)):
  `IDPoPProofReplayCache.TryRecordAsync` and `IClientAssertionReplayCache.TryRecordAsync` take an `OpenIdContext`
  first; the server no longer registers an in-memory `IDistributedCache`, so a host that resolved one registers its
  own; the record keys change, so records in flight across an upgrade are lost — they expire within minutes.
- New packages: `NCode.Identity.SharedState.Abstractions`, `NCode.Identity.SharedState`, and
  `NCode.Identity.SharedState.Valkey`.
- Amends [ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md) and
  [ADR-0102](0102-jwt-bearer-client-authentication.md) (where their nonce and replay records are kept), and
  [ADR-0134](0134-cloud-native-operating-principles.md) principle 4 (shared ephemeral state lives in the shared-state
  port, backed by Valkey).
