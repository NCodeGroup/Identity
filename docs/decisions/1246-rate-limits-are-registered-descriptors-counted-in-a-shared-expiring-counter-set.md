# 1246. Rate limits are registered descriptors counted in a shared expiring counter set

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md)

## Context

Under [project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md) a
correctness guard fails closed and a capacity guard fails open, behind a short timeout and a breaker; the family's rate
limiter is a capacity guard.

Customers run their dev, test and prod environments as separate tenants on one production platform
([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)),
so a load test against one tenant shares hardware with every other tenant's production traffic, and a tenant whose
request rate is unlimited can exhaust the capacity the others depend on.

A limit has to hold across replicas
([ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md)): a limiter whose state
lives in one process admits N times its limit on N replicas. The family's port for single-use state shared between
replicas, in-process by default with an opt-in Valkey adapter
([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)),
records presence, not counts.

A limit is judged per tenant: the authority above a scope sets its ceiling and a narrower scope only tightens it, as the
settings merge lattice says
([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)).
The limits are five: a per-minute limit per remote address, per tenant and per authenticated client, and a daily quota
of requests and of token requests, each quota with a bounded overage and a usage meter
([ADR-1198](1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md),
[ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md)). A package must be
able to add another limit, such as one per API key, by registering it, because a vocabulary is open unless provably
closed ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

Judged one by one, the limits would cost up to five sequential Valkey round trips before an endpoint runs, and a script
on a clustered Valkey may touch only keys that hash to one slot.

## Decision

**A rate limit or quota is a `RateLimitDescriptor` registered as data, a stage judges every descriptor registered for it
in one batched call to a shared expiring counter set, and a request over a limit is answered `429 Too Many Requests`.
The counter store fails open at a bounded cost.**

1. **A descriptor is a limit.** `RateLimitDescriptor` is a public sealed class in
   `NCode.Identity.OpenId.Authentication.Abstractions`, contributed through `ICollectionDataSource<RateLimitDescriptor>`
   like the descriptors of
   [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md). Its
   members:
   - `Name`, the unique partition, part of the counter key and the metric's `ncode.identity.rate_limiting.partition`
     attribute;
   - `Stage`, and `Order` within the stage;
   - `Window`, and `IsSliding`;
   - `LimitKey`, the `SettingKey<int>` that holds the limit, where an unset limit is unlimited;
   - `OverageKey`, the setting holding a fixed window's overage allowance;
   - `IsMeteredOnly`, which applies it only to a request its endpoint meters;
   - `UsageMeter` and `OverageUsageMeter`, the usage meters of an admitted request and of an overage;
   - `PartitionValueSelector`, a function of the `RateLimitContext`: null when the limit does not apply to the request,
     and an empty string when it counts every request in one partition.

   Every partition lies within the request's tenant, because the tenant is each counter key's hash tag.
2. **Stages are open names, and the limiter is public.** `RateLimitStages` names the three the server judges: `request`,
   judged by `RateLimitEndpointFilter` before the endpoint of every request to the OpenID endpoint group, after
   `OpenIdEnvironmentEndpointFilter` and before `OpenIdExceptionEndpointFilter`, so a refusal is not audited as a
   protocol error and a flood does not reach the audit log; `client_authentication`, judged by the client authentication
   service once per request after a confidential client authenticates, however often it authenticates, its refusal
   replacing the authentication result, so every endpoint that authenticates clients refuses alike and the client limit
   refuses before the token quota counts; and `token_request`, judged by the token endpoint for a confidential client.
   `IRequestRateLimiter.AcquireAsync(stage, context, cancellationToken)` and `RateLimitDecision` are public, so a
   package judges a stage of its own. Billing is the limiter's, not the endpoint's: once the endpoint has run, the
   endpoint filter records the meters of the admitting stages through `ITenantUsageRecorder`, each at most once, unless
   a stage refused the request.
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
   assigned, an IPv4-mapped address as its IPv4 address, and returns null without a remote address. The client
   selector counts only confidential clients, by client ID: a confidential client has proven its identity before the
   limit is judged, and a public client's `client_id` is public (the address limit covers its callers). The tenant and
   daily selectors count every request in one partition. Behind a proxy, a deployment must forward the client address
   for the address limit to count callers, not the proxy.
5. **The per-minute limits are settings.** Each is an integer of at least 1 with no default, a limit (`IsLimit`) merged
   with `Min`, and not discoverable. `rate_limit_client_per_minute` applies to tenants and clients, the others to
   tenants. The authority that writes a limit is
   [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)'s. The
   management API is not limited by tenant settings.
6. **Windows start at multiples of `Window`.** The multiples are counted from the first tick of `DateTimeOffset`, so a
   one-day window starts at midnight UTC. A sliding window keeps its counter for two windows and estimates
   `previous × (1 − elapsed / W) + current`, reading the previous counter with that weight; a rejected request is still
   counted, so a client that keeps retrying stays limited. A fixed window counts exactly, expires when it ends, and
   alone may carry an overage, which compares the exact count with the limit and with the limit plus the allowance. A
   limit is read from the client's effective settings when the context names a client, and from the tenant's otherwise.
7. **The counter port.** `IExpiringCounterSet` in `NCode.Identity.SharedState.Abstractions` has:
   - `IncrementAsync(key, expiresWhen)`, which atomically increments a counter and returns its count, starting an absent
     or expired counter at one with the given expiry (one fixed window); a past expiry records nothing and returns 0;
   - `GetAsync(key)`, which reads a count without incrementing it;
   - `RemoveAsync(key)`, which removes a counter, so a run of events that has ended counts afresh
     ([ADR-1237](1237-failed-password-attempts-are-limited-per-account-and-a-refusal-costs-what-a-wrong-password-costs.md));
   - `IncrementUntilExceededAsync(IReadOnlyList<ExpiringCounterIncrement>, CancellationToken)`, which increments the
     counters in order and stops after the first whose `Count + ReferenceWeight × ReferenceCount` exceeds its `Limit`.
     Each `ExpiringCounterIncrement` names a `Key`, `ExpiresWhen`, `Limit`, and optionally a `ReferenceKey` and
     `ReferenceWeight`. The reference counter is only read, and only at a positive weight. The call returns an
     `ExpiringCounterCount(Count, ReferenceCount)` per counter reached; fewer counts than increments mean the batch
     stopped at the last.

   A fixed window increments its counter against the limit plus the overage; a sliding window, against the limit, with
   the previous window's counter as the reference. The limiter judges each count admitted, overage or rejected.

   The in-process adapter (`AddInProcessExpiringCounterSet`, `Interlocked` counters) runs the loop sequentially and is
   correct for one replica. `AddValkey` registers `ValkeyExpiringCounterSet`, which runs the batch as one Lua script:
   `KEYS` holds two keys per counter (the counter and its reference) and `ARGV` three values per counter (the
   time-to-live in milliseconds, the weight and the limit). The script runs `INCR` and, when that starts the counter, `PEXPIRE`; reads
   the reference with `GET` when the weight is positive; and breaks after the first counter over its limit. The server
   runs it atomically, so increment and expiry are atomic, a counter never outlives its window, and a stage is one round
   trip. The replica's clock sets the time-to-live. `RemoveAsync` runs `DEL`.
8. **The tenant is the key's hash tag.** `ExpiringKeys.Create` builds tenant-scoped keys such as
   `rate-limit:{tenant-1}:…`. A clustered Valkey computes a key's slot from the text between its first `{` and the next
   `}`, so one tenant's keys share a slot and a script may touch several; a batch holds only one tenant's keys. The key
   holds a digest, so no client ID or address appears in the store. The Valkey adapter's `KeyPrefix` holds no braces,
   which would put every tenant in one slot.
9. **The response.** A refusal answers `429 Too Many Requests`
   ([RFC 6585 §4](https://www.rfc-editor.org/rfc/rfc6585#section-4)) with the `temporarily_unavailable` error code,
   `Retry-After` in whole seconds (rounded up, at least 1) until the estimate would admit another request or the fixed
   window ends, and `Cache-Control: no-store`; the internal `RateLimitErrors` builds it. The refusal is logged as
   `RateLimitExceeded(tenantId, partition, limit, window)`, never with an address or a client ID, and the request is
   never handled.
10. **An unreachable counter store fails open, at a bounded cost.** Unlike a single-use check, which fails closed, the
    capacity guard admits a request whose counter cannot be read, so a Valkey outage is not an authentication outage.
    Failing open is kept cheap, or the outage would cost every request the store client's own timeout once per stage:
    - Each store call is bounded to 100 ms; a call unanswered by then is abandoned and the request admitted.
    - A failed or late call opens a breaker: for 10 seconds the limiter admits every request uncounted without calling
      the store, then tries again. A further failure keeps it open another 10 seconds; only an answer to a call made
      after it opened closes it, so a slow earlier call cannot undo a newer failure. The breaker is timed on the
      monotonic clock, so a wall-clock step cannot hold it open.
    - The failure that opens the breaker logs one warning (`RateLimitStoreUnavailable`, naming the stage) and the
      closing answer one information message.
    - A request admitted without the store counts as `store_unavailable` and still adds its usage meters.
    - Valkey is a readiness dependency: the single-use key set fails closed without it.
11. **A catalog validates the descriptors.** The internal `IRateLimitDescriptorCatalog`, built on first use, throws an
    `InvalidOperationException` naming a misregistered descriptor: a blank or duplicate name, a blank stage, a window
    that is not positive, a missing selector, a key that names no registered `int` setting, a sliding window with an
    overage, an overage meter without an overage setting, or a usage meter longer than `OpenIdMaxLengths.UsageMeter`.
    Descriptors order by `Order`, then by ordinal `Name`.
12. **One wrapper measures every Valkey call.** `ValkeySharedStateMetrics.MeasureAsync`, in a generic and a non-generic
    overload, records every adapter command's duration, with `error.type` on an exception, which it rethrows. The batch
    is the operation `counter.increment_until_exceeded` of `ncode.identity.shared_state.operation`. Judgements count in
    `ncode.identity.rate_limiting.requests` by partition and result (`admitted`, `rejected`, `overage`,
    `store_unavailable`).

## Options considered

- **ASP.NET Core's built-in limiters.** Rejected: their state is per process, so a deployment's limit grows with its
  replica count, and their partition key is computed before the endpoint knows its tenant.
- **The client limit applied per address.** Rejected: clients behind one address (a NAT gateway or a backend) would be
  throttled as one; the address limit is a setting of its own.
- **A token bucket or GCRA script.** Rejected: it needs a script per algorithm and an exact in-process twin; the
  two-window estimate suffices for capacity and needs only the counter primitive.
- **Counting only admitted requests.** Rejected: a client that retries without backing off would be readmitted at every
  window start.
- **Closed partitions, a pipeline without a script, or increment-then-compensate.** Rejected: a closed set cannot gain a
  limit from a package (ADR-1150); a pipeline cannot stop at the first refusal, so an address refusal would still spend
  the tenant's allowance; compensation leaves an over-count that refuses other callers until it runs.
- **An `enum` of window kinds.** Rejected: a window is data, a length and whether it slides.
- **Chosen:** registered descriptors judged by stage in one batched, atomic call to a shared counter set.

## Consequences

- A stage costs one round trip to the counter store however many limits it judges; a package adds a limit by registering
  a descriptor, and a `request` limit adds a counter to the script, not a round trip.
- A package's stage judged inside an endpoint of the OpenID endpoint group is billed by the same filter; one judged
  elsewhere records its own usage through `ITenantUsageRecorder`.
- A deployment of several replicas must configure Valkey for its limits to hold; with the in-process adapter each
  replica enforces the limit alone.
- A brief Valkey outage lets traffic through unlimited for at least the breaker's 10 seconds, and costs at most one
  100 ms wait per replica per 10 seconds. Brute-force protection for credentials is a separate control.
- A misregistered descriptor fails the first protocol request, which builds the catalog, not the host's start.

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
- [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md) — registered descriptors and
  their catalog
- [ADR-1237](1237-failed-password-attempts-are-limited-per-account-and-a-refusal-costs-what-a-wrong-password-costs.md) — `RemoveAsync` and the password limiter
- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) — who
  writes a limit
- Code: `NCode.Identity.OpenId.Authentication.RateLimiting.RateLimitDescriptor`, `IRequestRateLimiter`,
  `DefaultRequestRateLimiter`, `DefaultRateLimitDescriptorCatalog`, `DefaultRateLimitDescriptorDataSource`,
  `RateLimitEndpointFilter`, `NCode.Identity.SharedState.IExpiringCounterSet`,
  `NCode.Identity.SharedState.Valkey.ValkeyExpiringCounterSet`
