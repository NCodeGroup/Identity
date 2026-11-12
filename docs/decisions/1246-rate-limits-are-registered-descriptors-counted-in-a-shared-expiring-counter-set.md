# 1246. Rate limits are registered descriptors counted in a shared expiring counter set

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md)

## Context

[project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md) has a
guard that protects correctness fail closed and a guard that protects only capacity fail open, behind a short timeout
and a breaker. The family's rate limiter is a capacity guard.

Customers run their dev, test and prod environments as separate tenants on one production platform
([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)),
so a load test against one tenant shares hardware with every other tenant's production traffic. Without a limit on a
tenant's request rate, one tenant can exhaust the capacity that every other tenant depends on.

A limit has to hold across replicas
([ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md)), so a limiter whose
state lives in one process admits N times its limit on N replicas, as ASP.NET Core's built-in limiters do. The family shares single-use state between replicas through a port with an in-process default and an opt-in
Valkey adapter
([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)),
but that port records presence, not counts.

A limit is judged per tenant: the authority above a scope sets its ceiling and a narrower scope may only tighten it, as
the settings merge lattice says
([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)).
The limits are five: a per-minute limit for a remote address, for a tenant and for an authenticated client, and a daily
quota for requests and for token requests, each quota with a bounded overage and a usage meter
([ADR-1198](1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md),
[ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md)). A package that needs
another limit, such as one per API key, must be able to add it by registering it, because a vocabulary is open unless it
is provably closed ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

Judging limits one by one would make up to five sequential calls to the shared counter set before an endpoint runs,
each a network round trip on Valkey, and a script on a clustered Valkey may touch only keys that hash to one slot.

## Decision

**A rate limit or quota is a `RateLimitDescriptor` registered as data, a stage judges every descriptor registered for it
in one batched call to a shared expiring counter set, and a request over a limit is answered `429 Too Many Requests`.
The counter store fails open at a bounded cost.**

1. **A descriptor is a limit.** `RateLimitDescriptor` is a public sealed class in
   `NCode.Identity.OpenId.Authentication.Abstractions`, contributed through `ICollectionDataSource<RateLimitDescriptor>`
   as the descriptors of
   [ADR-1207](1207-client-authentication-methods-are-named-by-registered-descriptors.md) are. Its members:
   - `Name`, the unique partition, which is a part of the counter key and the `ncode.identity.rate_limiting.partition`
     attribute of the metric;
   - `Stage`, and `Order` within the stage;
   - `Window`, and `IsSliding`;
   - `LimitKey`, the `SettingKey<int>` that holds the limit, where an unset limit is unlimited;
   - `OverageKey`, the setting that holds the overage allowance of a fixed window;
   - `IsMeteredOnly`, which limits the descriptor to a request that its endpoint meters;
   - `UsageMeter` and `OverageUsageMeter`, the usage meters of an admitted request and of an overage;
   - `PartitionValueSelector`, a function of the `RateLimitContext`: null when the limit does not apply to the request,
     and an empty string when it counts every request in one partition.

   Every partition lies within the request's tenant, because each counter key carries the tenant as its hash tag.
2. **Stages are open names, and the limiter is public.** `RateLimitStages` names the three that the server judges:
   `request`, judged by `RateLimitEndpointFilter` before the endpoint of every request to the OpenID endpoint group,
   after `OpenIdEnvironmentEndpointFilter` and before `OpenIdExceptionEndpointFilter`, so a refusal is not audited as a
   protocol error and a flood does not flood the audit log; `client_authentication`, judged once a confidential client
   has authenticated; and `token_request`, judged by the token endpoint for a confidential client.
   `IRequestRateLimiter.AcquireAsync(stage, context, cancellationToken)` and `RateLimitDecision` are public, so a
   package judges a stage of its own. The limiter, not the endpoint, records what an admitted request is billed: the
   endpoint filter records the meters of the admitting stages through `ITenantUsageRecorder` once the endpoint has run,
   unless a stage refused the request, and each meter at most once.
3. **The built-in limits are registered descriptors.** `DefaultRateLimitDescriptorDataSource` registers:

   | Name                   | Stage                   | Order | Window         | Limit setting                         |
   |------------------------|-------------------------|-------|----------------|---------------------------------------|
   | `address`              | `request`               | 100   | 1 min, sliding | `rate_limit_address_per_minute`       |
   | `tenant`               | `request`               | 200   | 1 min, sliding | `rate_limit_tenant_per_minute`        |
   | `daily_requests`       | `request`               | 300   | 1 day, fixed   | `quota_tenant_requests_per_day`       |
   | `client`               | `client_authentication` | 100   | 1 min, sliding | `rate_limit_client_per_minute`        |
   | `daily_token_requests` | `token_request`         | 100   | 1 day, fixed   | `quota_tenant_token_requests_per_day` |

   `daily_requests` is metered only, with the overage setting `quota_tenant_requests_overage_per_day` and the meters
   `requests` and `requests_overage`. `daily_token_requests` has the overage setting
   `quota_tenant_token_requests_overage_per_day` and the meters `token_requests` and `token_requests_overage`. The order
   makes the address limit refuse before the tenant limit counts, and no per-minute refusal spends a quota.
4. **Selectors.** The address selector counts an IPv6 address by its `/64`, the smallest block a host is usually
   assigned; it counts an IPv4-mapped address as the
   IPv4 address it maps; and it returns null for a request without a remote address. The client selector counts only
   confidential clients by client ID, because a confidential client has proven its identity before the limit is judged
   and a public client's `client_id` is public (the address limit covers its callers). The tenant and daily selectors
   count every request in one partition. A deployment behind a proxy must forward the client address
   for the address limit to count callers rather than the proxy.
5. **The per-minute limits are settings.** Each is an integer of at least 1 with no default, a limit (`IsLimit`) merged
   with `Min`, and not discoverable. `rate_limit_client_per_minute` applies to tenants and clients, the others to
   tenants. The authority that writes a limit is
   [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)'s. The
   management API is not limited by tenant settings.
6. **Windows start at multiples of `Window`.** A window starts at a multiple of `Window` counted from the first tick of
   `DateTimeOffset`, so a window of one day starts at midnight UTC. A sliding window keeps its counter for two windows
   and estimates `previous × (1 − elapsed / W) + current`; the previous window's counter is read with the weight
   `1 − elapsed / W`, and a rejected request is still counted, so a client that keeps retrying stays limited. A fixed
   window counts exactly, expires when it ends, and alone may carry an overage, which compares the exact count with the
   limit and with the limit plus the allowance. A limit is read from the client's effective settings when the context
   names a client, and from the tenant's settings otherwise.
7. **The counter port.** `IExpiringCounterSet` in `NCode.Identity.SharedState.Abstractions` has:
   - `IncrementAsync(key, expiresWhen)`, which atomically increments a counter and returns its count, starting an absent
     or expired counter at one with the given expiry (one fixed window); a past expiry records nothing and returns 0;
   - `GetAsync(key)`, which reads a count without incrementing it;
   - `RemoveAsync(key)`, which removes a counter, so the count of a run of events that has ended starts again
     ([ADR-1237](1237-failed-password-attempts-are-limited-per-account.md));
   - `IncrementUntilExceededAsync(IReadOnlyList<ExpiringCounterIncrement>, CancellationToken)`, which increments the
     counters in order and stops after the first whose `Count + ReferenceWeight × ReferenceCount` exceeds its `Limit`.
     Each `ExpiringCounterIncrement` names a `Key`, `ExpiresWhen`, `Limit`, and optionally a `ReferenceKey` and
     `ReferenceWeight`. The reference counter is only read, and only when its weight is positive. The call returns one
     `ExpiringCounterCount(Count, ReferenceCount)` for each counter it reached, so fewer counts than increments mean
     that the batch stopped at the last count.

   A fixed window increments its own counter against the limit plus the overage; a sliding window increments its own
   counter against the limit and names the previous window's counter as the reference. The limiter judges each count as
   admitted, overage or rejected.

   The in-process adapter (`AddInProcessExpiringCounterSet`, `Interlocked` counters) runs the loop sequentially and is correct for one replica. `AddValkey` registers
   `ValkeyExpiringCounterSet`, which runs the batch as one Lua script: `KEYS` holds two keys per counter, the counter
   and its reference, and `ARGV` holds three values per counter, the time-to-live in milliseconds, the weight and the
   limit. The script runs `INCR` and, when that starts the counter, `PEXPIRE`; reads the reference with `GET` when the
   weight is positive; and breaks after the first counter over its limit. The server runs it atomically, so the
   increment and the expiry are atomic and a counter never outlives its window, and a stage is one round trip. The
   time-to-live is computed from the replica's clock. `RemoveAsync` runs `DEL`.
8. **The tenant is the key's hash tag.** `ExpiringKeys.Create` builds tenant-scoped keys such as
   `rate-limit:{tenant-1}:…`. A clustered Valkey computes the slot of a key from the text between its first `{` and the
   next `}`, so every key of one tenant is in one slot and a script may touch several of them. A batch holds only one
   tenant's keys. No client ID or address appears in the store, because the key holds a digest. A `KeyPrefix` of the
   Valkey adapter holds no braces, which would put every tenant in one slot.
9. **The response.** A refusal answers `429 Too Many Requests`
   ([RFC 6585 §4](https://www.rfc-editor.org/rfc/rfc6585#section-4)) with the `temporarily_unavailable` error code,
   `Retry-After` in whole seconds (rounded up, and at least 1) until the estimate would admit one more request, or until
   the fixed window ends, and `Cache-Control: no-store`. `RateLimitErrors` is internal and builds the `429`. The refusal
   is logged as `RateLimitExceeded(tenantId, partition, limit, window)`, never with an address or a client ID, and the
   request is never handled.
10. **An unreachable counter store fails open, at a bounded cost.** Rate limiting protects capacity, not correctness:
    unlike a single-use check, which fails closed, a counter that cannot be read admits the request, so a Valkey outage
    does not become an authentication outage. Failing open is cheap, or the outage would still cost every request the
    store client's own timeout once per stage:
    - Each store call is bounded to 100 ms; a call not answered by then is abandoned and the request is admitted.
    - A failed or late call opens a breaker: the limiter stops calling the store for 10 seconds, admitting every request
      uncounted, then tries it again. A further failure keeps the breaker open for another 10 seconds; an answer closes
      it, but only an answer to a call made after the breaker opened, so a slow call that started earlier cannot undo a
      newer failure. The breaker is timed on the monotonic clock, so a wall-clock step cannot hold it open.
    - The failure that opens the breaker logs one warning (`RateLimitStoreUnavailable`, naming the stage) and the
      answer that closes it one information message.
    - A request admitted without the store counts as `store_unavailable` and still adds its usage meters.
    - Valkey is a readiness dependency: the single-use key set fails closed without it.
11. **A catalog validates the descriptors.** The internal `IRateLimitDescriptorCatalog`, built on first use, throws an
    `InvalidOperationException` naming a misregistered descriptor: a blank or duplicate name, a blank stage, a window
    that is not positive, a missing selector, a key that names no registered `int` setting, a sliding window with an
    overage, an overage meter without an overage setting, or a usage meter longer than `OpenIdMaxLengths.UsageMeter`.
    Descriptors order by `Order`, then by ordinal `Name`.
12. **One wrapper measures every Valkey call.** `ValkeySharedStateMetrics.MeasureAsync`, in a generic and a non-generic
    overload, records the duration of every adapter command, and on an exception records it with `error.type` and
    rethrows. The batch is the operation `counter.increment_until_exceeded` of
    `ncode.identity.shared_state.operation`. Judgements count in `ncode.identity.rate_limiting.requests` by partition and
    result (`admitted`, `rejected`, `overage`, `store_unavailable`).

## Options considered

- **ASP.NET Core's built-in limiters.** Rejected: their state is per process, so a deployment's limit grows with its
  replica count, and their partition key is computed before the endpoint knows its tenant.
- **A token bucket or GCRA script.** Rejected: it needs a script per algorithm and an exact in-process twin; the
  two-window estimate suffices for capacity protection and needs only the counter primitive.
- **Counting only admitted requests.** Rejected: a client that retries without backing off would be admitted again at
  every window start.
- **Closed partitions, a pipeline without a script, or increment-then-compensate.** Rejected: a closed set cannot gain a
  limit from a package (ADR-1150); a pipeline cannot stop at the first refusal, so an address refusal would still spend
  the tenant's allowance; compensation leaves an over-count that refuses other callers until it runs.
- **An `enum` of window kinds.** Rejected: a window is data, a length and whether it slides.
- **Chosen:** registered descriptors judged by stage in one batched, atomic call to a shared counter set.

## Consequences

- A stage costs one round trip to the counter store however many limits it judges, and a package adds a limit by
  registering a descriptor: a limit of the `request` stage adds a counter to the script, not a round trip.
- A deployment that runs several replicas must configure Valkey for its limits to hold; with the in-process adapter each
  replica enforces the limit alone.
- A brief Valkey outage lets traffic through unlimited for at least the 10 seconds the breaker stays open, and costs at
  most one 100 ms wait per replica per 10 seconds. Brute-force protection for credentials is a separate control.
- A misregistered descriptor fails the first protocol request, when the catalog is built, rather than the start of the
  host.

## References

- [RFC 6585 §4](https://www.rfc-editor.org/rfc/rfc6585#section-4) — `429 Too Many Requests`
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)
  — an unset limit is unlimited
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open
- [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)
  — the shared expiring key set and its tenant-scoped keys
- [ADR-1198](1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md) — daily quotas and the
  bounded overage
- [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md) — the usage ledger
- [ADR-1207](1207-client-authentication-methods-are-named-by-registered-descriptors.md) — registered descriptors and
  their catalog
- [ADR-1237](1237-failed-password-attempts-are-limited-per-account.md) — `RemoveAsync` and the password limiter
- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) — who
  writes a limit
- Code: `NCode.Identity.OpenId.Authentication.RateLimiting.RateLimitDescriptor`, `IRequestRateLimiter`,
  `DefaultRequestRateLimiter`, `DefaultRateLimitDescriptorCatalog`, `DefaultRateLimitDescriptorDataSource`,
  `RateLimitEndpointFilter`, `NCode.Identity.SharedState.IExpiringCounterSet`,
  `NCode.Identity.SharedState.Valkey.ValkeyExpiringCounterSet`
