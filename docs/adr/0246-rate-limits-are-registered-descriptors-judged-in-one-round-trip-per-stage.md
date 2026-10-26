# 246. Rate limits are registered descriptors judged in one round trip per stage

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

[ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md),
[ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md),
[ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) and
[ADR-0200](0200-a-tier-admits-a-bounded-overage-past-its-quota-and-bills-it-from-the-ledger.md) define five limits: a
per-minute limit for a remote address, for a tenant and for an authenticated client, and a daily quota for requests and
for token requests, each quota with a bounded overage and a usage meter. The limiter holds them in code. Each has its
own method, window, settings and call site, and the endpoint filter and the token endpoint record the usage meters
beside the limit. A package that needs another limit, such as one per API key, has nowhere to put it, although a
vocabulary is open unless it is provably closed
([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).

The shape also costs round trips. A sliding window takes an increment and a read of the previous window, and a quota an
increment, so a request to a metered endpoint makes up to five sequential calls to the shared counter set before its
endpoint runs. On Valkey each is a network round trip, and each adapter method repeats the same timing and error
recording around its command.

A clustered Valkey adds a constraint: a script may touch only keys that hash to one slot, so a batch over several
counters needs a key layout that keeps them together.

## Decision

**A rate limit or quota is a `RateLimitDescriptor` registered as data, a stage judges every descriptor registered for
it in one batched call to the counter store, and the limiter, not the endpoint, records what an admitted request is
billed.**

1. **A descriptor is a limit.** `RateLimitDescriptor` is a public sealed class in
   `NCode.Identity.OpenId.Authentication.Abstractions`, in the namespace
   `NCode.Identity.OpenId.Authentication.RateLimiting`, contributed through
   `ICollectionDataSource<RateLimitDescriptor>` as the descriptors of
   [ADR-0207](0207-client-authentication-methods-are-named-by-registered-descriptors.md) are. Its members:
   - `Name`: the partition, which is a part of the counter key, the `ncode.identity.rate_limiting.partition` attribute
     of the metric, and unique among the registered descriptors;
   - `Stage`, and `Order` within the stage;
   - `Window`, and `IsSliding`;
   - `LimitKey`, the `SettingKey<int>` that holds the limit, where an unset limit is unlimited
     ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md));
   - `OverageKey`, the setting that holds the overage allowance of a fixed window;
   - `IsMeteredOnly`, which limits the descriptor to a request that its endpoint meters;
   - `UsageMeter` and `OverageUsageMeter`, the usage meters of an admitted request and of an overage;
   - `PartitionValueSelector`, a function of the `RateLimitContext` that returns the partition's value, null when the
     limit does not apply to the request, and an empty string when the limit counts every request in one partition.
     Every partition lies within the request's tenant, because each counter key carries the tenant as its hash tag
     (item 6), so a descriptor cannot count requests across tenants.

   `RateLimitContext` carries the `OpenIdContext` and, for a stage that follows client authentication, the confidential
   client.
2. **Stages are open names, and the limiter is public.** `RateLimitStages` names the three that the server judges:
   `request`, judged by `RateLimitEndpointFilter` before the endpoint of every request to the OpenID endpoint group;
   `client_authentication`, judged once a confidential client has authenticated; and `token_request`, judged by the
   token endpoint for a confidential client. `IRequestRateLimiter.AcquireAsync(stage, context, cancellationToken)` and
   `RateLimitDecision` are public, so a package that registers descriptors for a stage of its own judges it. The limiter
   marks a request that it refuses, and adds the usage meters of a stage that admits the request, to an internal
   `RateLimitRequestState` in `HttpContext.Items`. `RateLimitEndpointFilter` records the meters through
   `ITenantUsageRecorder` once the endpoint has run, unless a stage refused the request, so a request that a later stage
   refuses is never billed ([ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md)). A request records
   each usage meter at most once, however many descriptors or stages add it. A meter is a descriptor's `UsageMeter` or
   `OverageUsageMeter`; no code records one beside a limit. `RateLimitErrors` stays internal and only builds the `429`.
3. **The built-in limits are registered descriptors.** `DefaultRateLimitDescriptorDataSource` registers the five with
   the settings, keys and metrics of ADR-0196, ADR-0198 and ADR-0200:

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
   keeps the guarantees of the earlier decisions: the address limit refuses before the tenant limit counts, a per-minute
   refusal never spends a daily quota, the client limit refuses before the token quota counts, and public metadata such
   as discovery and the key set never spends the request quota. The address selector counts an IPv6 address by its `/64`
   and an IPv4-mapped address as the IPv4 address it maps, and returns null for a request without a remote address. The
   client selector returns the client ID, and null for a request whose client is public, because a public client is
   not in the context. The other three count every request in one partition.
4. **Windows start at multiples of their length.** A window starts at a multiple of `Window` counted from the first tick
   of `DateTimeOffset`, so a window of one day starts at midnight UTC. A sliding window keeps the estimate of ADR-0196:
   its counter lives for two windows, and the previous window's counter is read with the weight
   `1 − elapsed / window`. A fixed window counts exactly and expires when it ends. Only a fixed window may carry an
   overage, because the overage compares the exact count with the limit and with the limit plus the allowance
   (ADR-0200). A limit is read from the client's effective settings when the context names a client, because they merge
   the tenant's, and from the tenant's settings otherwise.
5. **A stage is one call to the counter store.**
   `IExpiringCounterSet.IncrementUntilExceededAsync(IReadOnlyList<ExpiringCounterIncrement>, CancellationToken)`
   increments the counters in order and stops after the first whose `Count + ReferenceWeight × ReferenceCount` exceeds
   its `Limit`. Each `ExpiringCounterIncrement` names a `Key`, an `ExpiresWhen`, a `Limit`, and optionally a
   `ReferenceKey` and a `ReferenceWeight`. The counter is incremented as `IncrementAsync` does, so an expiry that is not
   in the future counts nothing and reports zero, and the reference counter is only read, and only when its weight is
   positive. The call returns one `ExpiringCounterCount(Count, ReferenceCount)` for each counter it reached, so fewer
   counts than increments mean that the batch stopped at the last count. A fixed window increments its own counter with
   the limit plus the overage as its limit; a sliding window increments its own counter with the limit, and names the
   previous window's counter as the reference. The limiter then judges each count as admitted, overage or rejected, and
   a refusal answers `Retry-After` until the estimate would admit one more request, or until the fixed window ends.
   The Valkey adapter runs the batch as one Lua script. `KEYS` holds two keys per counter, the counter and its
   reference, and `ARGV` holds three values per counter, the time-to-live in milliseconds, the weight and the limit. The
   script runs `INCR` and, when that starts the counter, `PEXPIRE`; reads the reference with `GET` when the weight is
   positive; and breaks after the first counter over its limit. The server runs it atomically. The in-process adapter
   runs the same loop sequentially. The single-key operations stay, and the password limiter uses them
   ([ADR-0237](0237-failed-password-attempts-are-limited-per-account.md)).
6. **The tenant is the key's hash tag.** `ExpiringKeys.Create` returns `{purpose}:{tenant in braces}:{digest}`, for
   example `rate-limit:{tenant-1}:…`. A clustered Valkey computes the slot of a key from the text between its first `{`
   and the next `}`, so every key of one tenant is in one slot and a script may touch several of them. A batch holds
   only one tenant's keys.
7. **One wrapper measures every Valkey call.** `ValkeySharedStateMetrics.MeasureAsync`, in a generic and a non-generic
   overload, takes the start timestamp, awaits the adapter's command, records the duration of the call, and on an
   exception records it with `error.type` and rethrows. Every adapter method calls it, and `OperationCompleted` is
   private. The batch is the operation `counter.increment_until_exceeded` of `ncode.identity.shared_state.operation`.
8. **A catalog validates the descriptors when it is first built.** The internal `IRateLimitDescriptorCatalog` is built
   by dependency injection on first use, as `DefaultClientAuthenticationMethodCatalog` is. It reads every data source
   once and throws an `InvalidOperationException` that names the descriptor when:
   - a name or a stage is empty or contains whitespace, or two descriptors share a name;
   - a window is not positive, or a selector is missing;
   - a limit or an overage key does not name a registered `int` setting;
   - a sliding window has an overage, or an overage meter has no overage setting;
   - a usage meter is empty or longer than `OpenIdMaxLengths.UsageMeter` characters.

   The descriptors of a stage are ordered by `Order`, then by `Name` with the ordinal comparison.
9. **Logs name the limit.** `RateLimitExceeded(tenantId, partition, limit, window)` serves every limit, and logs the
   tenant, the partition's name, the limit and the window, never an address or a client ID. `QuotaExceeded` is removed
   and its event ID, `2044`, stays retired. `RateLimitStoreUnavailable` names the stage. The fail-open policy of
   ADR-0196 holds for a stage: a counter store that throws or does not answer within 100 ms admits the request without
   counting it, still adds its usage meters, and is not called again for 10 seconds.

## Options considered

- **Keep the fixed partitions.** Rejected: the vocabulary is closed, so a package cannot add a limit, against ADR-0150.
- **Pipeline the commands without a script.** Rejected: a pipeline cannot stop at the first refusal, so a request that
  the address limit refuses would still spend the tenant's allowance and the daily quota.
- **Increment everything, then compensate with decrements.** Rejected: until the decrement runs, the counters hold an
  over-count that refuses other callers, and a replica that stops between the two leaves it for the rest of the window.
- **A closed `enum` of window kinds.** Rejected: a window is data, a length and whether it slides, and an `enum` would
  need a member for every length.
- **A script for each algorithm, such as a token bucket.** Rejected for the reasons of ADR-0196: the estimate over two
  fixed windows needs only the counter primitive, and every algorithm would need its own state and script.

## Consequences

- A stage costs one round trip to the counter store however many limits it judges; a call for each limit would cost up
  to five for a request to a metered endpoint.
- A package adds a limit by registering a descriptor, and a limit of the `request` stage adds a counter to the script,
  not a round trip.
- **Breaking:** every `IExpiringCounterSet` implementation implements `IncrementUntilExceededAsync`.
- The key format changes, so counters and replay records in flight across an upgrade start over: a window's count
  restarts at zero, and a proof or assertion seen just before the upgrade can be accepted once more until it expires.
- A `KeyPrefix` of the Valkey adapter that holds braces would become the hash tag of every key and put every tenant in
  one slot, so a prefix holds none.
- A misregistered descriptor fails the first protocol request, when the catalog is built, rather than the start of the
  host.
- A stage that a package judges inside an endpoint of the OpenID endpoint group bills the request through the same
  filter; a package that judges a stage elsewhere records its own usage.
- This implements issue [#232](https://github.com/NCodeGroup/Identity/issues/232).

## References

- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — an unset limit is unlimited.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open.
- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) — the shared expiring key set
  and its tenant-scoped keys.
- [ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) — the counter set and the sliding
  estimate.
- [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) — the rate-limiting and
  shared-state metrics.
- [ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md) — daily quotas.
- [ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) — the usage ledger.
- [ADR-0200](0200-a-tier-admits-a-bounded-overage-past-its-quota-and-bills-it-from-the-ledger.md) — the bounded
  overage.
- [ADR-0207](0207-client-authentication-methods-are-named-by-registered-descriptors.md) — registered descriptors and
  their catalog.
- Code: `NCode.Identity.OpenId.Authentication.RateLimiting.RateLimitDescriptor`,
  `NCode.Identity.OpenId.Authentication.RateLimiting.IRequestRateLimiter`,
  `NCode.Identity.OpenId.Authentication.RateLimiting.DefaultRequestRateLimiter`,
  `NCode.Identity.OpenId.Authentication.RateLimiting.DefaultRateLimitDescriptorCatalog`,
  `NCode.Identity.OpenId.Authentication.RateLimiting.DefaultRateLimitDescriptorDataSource`,
  `NCode.Identity.OpenId.Authentication.RateLimiting.RateLimitEndpointFilter`,
  `NCode.Identity.OpenId.Authentication.SharedState.ExpiringKeys`,
  `NCode.Identity.SharedState.IExpiringCounterSet`, `NCode.Identity.SharedState.Valkey.ValkeyExpiringCounterSet`,
  `NCode.Identity.SharedState.Valkey.ValkeySharedStateMetrics`.
