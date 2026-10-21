# 196. Tenants are rate limited through a shared expiring counter set

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

Customers run their dev, test, and prod environments as separate tenants on one production platform
([hosting analysis](../hosting-analysis.md#environment-model)), so a load test against one tenant shares hardware with
every other tenant's production traffic. Nothing limits a tenant's request rate, so one tenant can exhaust the capacity
that every other tenant depends on.

A limit has to hold across replicas. Any replica may serve the next request
([ADR-0134](0134-cloud-native-operating-principles.md)), so a limiter whose state lives in one process admits N times
its limit on N replicas. ASP.NET Core's built-in limiters (`FixedWindowRateLimiter`, `SlidingWindowRateLimiter`,
`TokenBucketRateLimiter`) keep their state in process. The family already shares single-use state between replicas
through a port with an in-process default and an opt-in Valkey adapter
([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)), but that port records
presence, not counts.

A limit also has to be judged per tenant: the server operator sets the platform's ceiling, and a tenant (or a plan tier
applied to it) may tighten it but never loosen it. That is the settings cascade's merge lattice
([ADR-0038](0038-setting-merge-lattice-duals.md)).

## Decision

**Requests are counted in a shared, atomic expiring counter set, partitioned by tenant and then by client, against
limits that are settings bounded by the merge lattice; a request over a limit is answered `429 Too Many Requests` with
`Retry-After`.**

1. **The port.** `NCode.Identity.SharedState.Abstractions` defines `IExpiringCounterSet`:
   `IncrementAsync(key, expiresWhen)` atomically increments a counter and returns its count, starting an absent or
   expired counter at one with the given expiry and leaving the expiry of a live counter unchanged (one fixed window);
   `GetAsync(key)` reads a count without incrementing it. A counter whose expiry is not in the future is not recorded,
   and the increment returns zero.
2. **The adapters.** `AddInProcessExpiringCounterSet` registers the in-process default (a concurrent dictionary of
   `Interlocked` counters with the key set's periodic sweep), correct for one replica; `AddIdentityServer` registers it
   beside the in-process key set. `AddValkey` replaces it with `ValkeyExpiringCounterSet`, which runs one script —
   `INCR`, then `PEXPIRE` when the increment started the counter — so the increment and the expiry are atomic on the
   server and a counter never outlives its window. The time-to-live is computed from the replica's clock, as the key set
   does.
3. **The algorithm is a sliding-window estimate over two fixed windows.** For a limit of `L` requests per window `W`, a
   request increments the counter of the current window (keyed by its start, and kept for two windows so it can serve as
   the previous one) and reads the previous window's count. The estimate is `previous × (1 − elapsed / W) + current`;
   the request is rejected when the estimate exceeds `L`. This bounds the burst a fixed window allows at its boundary,
   costs one script and one read per request, and needs only the counter primitive. A rejected request is still counted,
   so a client that keeps retrying stays limited.
4. **Partitions.** A tenant-wide limit applies to every request the tenant serves. Within the tenant, a per-client limit
   applies to the authenticated client, or to the remote address when the request authenticates none (the authorization
   endpoint, discovery, and the JWKS). The per-client limit is judged only once the client has authenticated, so a
   request cannot spend another client's allowance by naming its `client_id`. Keys are
   `rate-limit:{tenantId}:{partition}:{windowStart}`.
5. **Limits are settings.** The limits are integer settings that apply at the server and tenant scopes (the per-client
   limit also at the client scope), merged with `Min`, so a tenant or client can only tighten the ceiling the scope
   above it sets. An unset limit is unlimited ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)).
6. **Where it runs.** An endpoint filter on the OpenID endpoint group runs after `OpenIdEnvironmentEndpointFilter`, so
   the resolved tenant and its merged settings are known, and judges the tenant-wide and per-address limits there; the
   per-client limit is judged where client authentication succeeds. The management API is not limited by tenant
   settings; it authenticates against the control-plane tenant
   ([ADR-0191](0191-the-management-api-authenticates-against-the-control-plane-tenant.md)).
7. **The response.** A rejected request is answered `429 Too Many Requests`
   ([RFC 6585 §4](https://www.rfc-editor.org/rfc/rfc6585#section-4)) with `Retry-After` in whole seconds until the
   current window ends, rounded up, and `Cache-Control: no-store`. The server records a metric and logs the rejection; a
   rejected request is never handled.
8. **An unreachable counter store fails open.** Rate limiting protects capacity, not correctness: unlike a single-use
   check, which fails closed (ADR-0164), a counter that cannot be read admits the request and logs a warning, so a
   Valkey outage does not become an authentication outage.
9. **Quotas are a separate decision.** Daily request and token-issuance quotas for plan tiers count over far longer
   windows and need billing semantics (reset time zone, overage handling); they reuse the counter port but are decided
   separately.

## Options considered

- **ASP.NET Core's built-in limiters.** Rejected: their state is per process, so a deployment's limit grows with its
  replica count, and their partition key is computed before the endpoint knows its tenant.
- **A token bucket or GCRA in a Valkey script.** Smoother than a two-window estimate and one round trip, but it needs a
  read-modify-write script per algorithm and an in-process twin that matches it exactly. The two-window estimate is
  accurate enough for capacity protection and reuses one simple primitive.
- **Counting only admitted requests.** Rejected: a client that retries without backing off would be admitted again at
  the start of every window, so the limit would not slow it down.
- **Failing closed when the store is unreachable.** Rejected: it turns a cache outage into a full outage for every
  tenant, which is the harm the limiter exists to prevent.

## Consequences

- The counter port, both adapters, and the host's default registration exist. The endpoint filter, the limit settings,
  the `429` response, and the integration tests are tracked in [`BACKLOG.md`](../../BACKLOG.md) as `F45`; quotas are
  `F79`.
- A deployment that runs several replicas must configure Valkey for its limits to hold; with the in-process adapter
  each replica enforces the limit alone.
- A brief Valkey outage lets traffic through unlimited. Brute-force protection for credentials is a separate control
  and does not depend on this limiter.

## References

- [RFC 6585 §4](https://www.rfc-editor.org/rfc/rfc6585#section-4) — `429 Too Many Requests`
- [RFC 9110 §10.2.3](https://www.rfc-editor.org/rfc/rfc9110#section-10.2.3) — `Retry-After`
- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) — the shared key set
- [ADR-0038](0038-setting-merge-lattice-duals.md) — the setting merge lattice
- `src/NCode.Identity.SharedState.Abstractions/IExpiringCounterSet.cs`,
  `src/NCode.Identity.SharedState/InProcess/DefaultInProcessExpiringCounterSet.cs`,
  `src/NCode.Identity.SharedState.Valkey/ValkeyExpiringCounterSet.cs`
