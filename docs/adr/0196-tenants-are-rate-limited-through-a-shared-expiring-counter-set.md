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

**Requests are counted in a shared, atomic expiring counter set, partitioned by tenant and then by remote address and by
client, against per-minute limits that are settings bounded by the merge lattice; a request over a limit is answered
`429 Too Many Requests` with `Retry-After`.**

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
3. **The algorithm is a sliding-window estimate over two fixed windows.** For a limit of `L` requests per window `W` —
   one minute, so the limits read as requests per minute — a request increments the counter of the current window (keyed
   by its start, and kept for two windows so it can serve as the previous one) and reads the previous window's count.
   The estimate is `previous × (1 − elapsed / W) + current`; the request is rejected when the estimate exceeds `L`. This
   bounds the burst a fixed window allows at its boundary, costs one script and one read per request, and needs only the
   counter primitive. A rejected request is still counted, so a client that keeps retrying stays limited.
4. **Partitions.** Three independent partitions, each with its own limit:
   - **tenant** — every request the tenant's protocol endpoints serve;
   - **address** — every request from one remote address, including the ones that authenticate no client (the
     authorization endpoint, discovery, and the JWKS). An IPv6 address is counted by its `/64`, the smallest block a
     host is usually assigned, so one caller cannot spread its requests across its own addresses; an IPv4-mapped
     address counts as the IPv4 address it maps;
   - **client** — every request that authenticates as one confidential client. It is judged only once the client has
     proven its identity, so a request cannot spend another client's allowance by naming its `client_id`; a public
     client is named by its `client_id` alone, so it is not counted (the address limit covers its callers). It is
     counted once per request however many times the request's client is authenticated.

   The address limit is judged before the tenant-wide limit, and a request it refuses is not counted against the
   tenant, so one address over its own limit cannot spend the allowance every other caller of the tenant shares.

   The address limit is a separate setting rather than the client limit applied by address: many clients may share an
   address (a NAT gateway or a backend), so a per-client figure would throttle all of them as one. Keys are built with
   the family's tenant-scoped key builder from `rate-limit`, the tenant, the partition, the partition's value (empty,
   the address, or the client ID), and the window's start, so a client ID or address never appears in the store.
5. **Limits are settings.** `rate_limit_tenant_per_minute` and `rate_limit_address_per_minute` apply at the tenant scope
   and `rate_limit_client_per_minute` at the tenant and client scopes, so a tenant sets every client's ceiling and a
   client may lower its own. Each is an integer of at least one with no default, merged with `Min`, so a narrower scope
   can only tighten the ceiling the scope above it sets — the server's settings cascade into every tenant. An unset
   limit is unlimited ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)). The limits are not
   discoverable.
6. **Where it runs.** `RateLimitEndpointFilter` on the OpenID endpoint group runs after
   `OpenIdEnvironmentEndpointFilter`, so the resolved tenant and its merged settings are known, and judges the
   address limit and then the tenant-wide limit there. It runs before `OpenIdExceptionEndpointFilter`, so a refused
   request is not audited as a protocol error and an attacker who floods an endpoint does not flood the audit log as
   well. The per-client limit is judged in the client authentication service when a client authenticates; the refusal
   replaces the authentication result, so every endpoint that authenticates clients refuses the request the same way.
   The management API is not limited by tenant settings; it authenticates against the control-plane tenant
   ([ADR-0191](0191-the-management-api-authenticates-against-the-control-plane-tenant.md)).
7. **The response.** A rejected request is answered `429 Too Many Requests`
   ([RFC 6585 §4](https://www.rfc-editor.org/rfc/rfc6585#section-4)) with the `temporarily_unavailable` error code,
   `Retry-After` in whole seconds (rounded up, and at least one) until the estimate would admit one more request — later
   in the current window as the previous window's weight decays, or into the next window when the current window alone
   is at the limit — and
   `Cache-Control: no-store`. The server logs the rejection with a stable event ID (the tenant, the partition, and the
   limit, never the address or client ID); a rejected request is never handled.
8. **An unreachable counter store fails open, at a bounded cost.** Rate limiting protects capacity, not correctness:
   unlike a single-use check, which fails closed (ADR-0164), a counter that cannot be read admits the request, so a
   Valkey outage does not become an authentication outage. Failing open must also be cheap, or the outage still costs
   every request the store client's own timeout (about 5 s with StackExchange.Redis) once per limit judged:
   - Each counter call is bounded to **100 ms**. A call that has not answered by then is abandoned, and the request is
     admitted. A call that has already completed, as every in-process call has, starts no timer.
   - A failed or late call **opens a breaker**: the limiter stops calling the store for **10 s**, admitting every
     request uncounted, then tries it again. A further failure keeps the breaker open for another 10 s; an answer
     closes it. An outage therefore costs at most one 100 ms wait per replica per 10 s, not one per check.
   - The failure that opens the breaker logs **one warning** with the exception, and the answer that closes it logs one
     information message, so an outage is logged once per replica rather than once per request. Every request admitted
     without the store is still counted as `store_unavailable`.
   - Valkey **stays a readiness dependency.** The single-use key set fails closed without it, so a replica that cannot
     reach Valkey cannot redeem a code or refresh token; it reports not ready, as it does when the database is
     unreachable. The limiter's breaker covers the time before readiness takes the replica out of service, and a
     store that is slow but still answering.
9. **Quotas are a separate decision.** Daily request and token quotas for plan tiers count over far longer windows and
   need billing semantics (reset time zone, overage handling); they reuse the counter port and are decided in
   [ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md).

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

- Every protocol endpoint is limited once a tenant sets a limit; nothing is limited by default. Daily quotas are
  [ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md).
- Every judged partition is counted in `ncode.identity.rate_limiting.requests` by partition and result (`admitted`,
  `rejected`, `store_unavailable`), per
  [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md), and the `429` itself
  is visible on `http.server.request.duration` with `ncode.identity.openid.error=temporarily_unavailable`.
- A deployment behind a proxy must forward the client's address (`UseForwardedHeaders`) for the address limit to count
  callers rather than the proxy.
- A deployment that runs several replicas must configure Valkey for its limits to hold; with the in-process adapter
  each replica enforces the limit alone.
- A brief Valkey outage lets traffic through unlimited, for at least the 10 s the breaker stays open. Brute-force
  protection for credentials is a separate control and does not depend on this limiter.
- The timeout and the retry interval are fixed, not settings: they bound the cost of an outage rather than express a
  tenant's policy.

## References

- [RFC 6585 §4](https://www.rfc-editor.org/rfc/rfc6585#section-4) — `429 Too Many Requests`
- [RFC 9110 §10.2.3](https://www.rfc-editor.org/rfc/rfc9110#section-10.2.3) — `Retry-After`
- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) — the shared key set
- [ADR-0038](0038-setting-merge-lattice-duals.md) — the setting merge lattice
- `src/NCode.Identity.SharedState.Abstractions/IExpiringCounterSet.cs`,
  `src/NCode.Identity.SharedState/InProcess/DefaultInProcessExpiringCounterSet.cs`,
  `src/NCode.Identity.SharedState.Valkey/ValkeyExpiringCounterSet.cs`,
  `src/NCode.Identity.OpenId.Authentication/RateLimiting/`
