# 1198. A daily quota counts a UTC day and a tier may admit a bounded billed overage

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

Per-minute rate limits ([ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md))
protect capacity from bursts, but they do not bound what a tenant consumes over a day. The hosted platform sells plan
tiers, and a tier is defined by how much a tenant may use: requests to its protocol endpoints and token requests.
Customers also run their dev and test environments as tenants on the production platform
([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)),
so a load test that stays under the per-minute limit for hours still has to stop somewhere.

A quota must hold across replicas, as a limit does, and the family already has the primitive: the shared, atomic
`IExpiringCounterSet` with its in-process default and Valkey adapter. Four questions arise: the window (a calendar day
in UTC, or a rolling 24 hours), what happens at the quota (refuse, or admit and bill an overage), how far an admitting
tier may go, and how the overage reaches the customer. A tier that admits an overage needs a durable usage record to
bill from, which the usage ledger of [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md) is.

## Decision

**A quota is one fixed window per UTC calendar day, counted exactly in the shared counter set and expiring at the next
midnight UTC. A tier opts in to an overage by setting an allowance per quota, and the allowance is the hard ceiling.**

1. **The window is the UTC calendar day.** A tenant's count starts at midnight UTC and its counter expires at the next
   midnight. A calendar day is what a plan states and what an operator can explain ("resets at 00:00 UTC"), it costs
   one atomic increment per request, and it needs no estimate: bursts are the per-minute limits' job. UTC, not the
   tenant's time zone, so every replica and every tenant agree on the boundary.
2. **Two quotas, both tenant settings.**
    - `quota_tenant_requests_per_day` (descriptor `daily_requests`, request stage, order 300, metered only) counts
      requests to the tenant's metered protocol endpoints. It is judged after the address and tenant per-minute limits,
      so a request a limit refuses does not spend the quota. The discovery document and the JSON Web Key Set are not
      metered (`IOpenIdEndpointMeteredMetadata`, set with `WithOpenIdMetered(false)`): they are public metadata that
      any anonymous caller, cache or relying-party library fetches, so counting them would let anyone spend the quota,
      run up the overage and refuse the tenant's clients until midnight. Metering is endpoint metadata, so a package
      that adds another public metadata endpoint opts it out the same way
      ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
    - `quota_tenant_token_requests_per_day` (descriptor `daily_token_requests`) counts token requests from
      confidential clients, judged at the token endpoint after client authentication and before the grant is
      processed. A public client is named by its `client_id` alone, which anyone can read from an authorization URL,
      so counting it would let any caller spend the quota and refuse every client of the tenant until midnight;
      public clients are bounded by the request quota. It counts requests, not issued tokens: the count is
      incremented before the request is judged, which is atomic across replicas, while counting issued tokens would
      need a read before the grant and an increment after it, which lets concurrent replicas overshoot.

    Each quota is a tenant-scope integer of at least `1` with no default, a limit merged with `Min`, where unset
    means unlimited ([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)).
    Quotas are not discoverable.
3. **Each quota has an overage allowance.** `quota_tenant_requests_overage_per_day` and
   `quota_tenant_token_requests_overage_per_day` are the most requests (or confidential clients' token requests) a
   tenant is served past the matching quota in one UTC calendar day. They are integers of at least `0`, limits merged
   with `Min`. Setting the allowance is the opt-in, and its value is the hard ceiling. An allowance that is unset or
   `0` refuses at the quota. Unset means none here, unlike the unrestricted default of ADR-1010: an allowance creates
   a charge, and a missing setting must never bill a customer without limit. A tier that wants effectively unlimited
   overage sets a large value. An allowance without its quota does nothing.
4. **One day counter judges both.** The fixed window's limit is the quota plus the allowance, and a count past the
   quota is admitted as `overage` (the metric result `overage`), so the overage needs no second counter and stays
   exact across replicas.
5. **At the ceiling the request is refused** with `429 Too Many Requests`, the `temporarily_unavailable` error code,
   `Retry-After` in whole seconds until midnight UTC and `Cache-Control: no-store`: the response a per-minute limit
   gives, so a client handles both the same way.
6. **The overage is billed from the ledger as its own meter.** An admitted overage request is recorded under
   `requests_overage` or `token_requests_overage` in addition to `requests` or `token_requests`, so the plain meters
   are the total served and the overage meters are exactly what is billable. A request that a later stage refuses (the
   per-client limit or the token quota) after the request quota admitted it as overage is not recorded, so it is not
   billed. The usage report and the console's Usage tab list the overage meters with the other meters, so no endpoint
   of its own is needed.
7. **A plan tier is a set of limits** that the platform's administrators write on a team's document, and a team's
   administrators may write lower ones on its tenants
   ([ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)). A
   tier is not a setting scope.
8. **A counter store that fails admits the request** and logs a warning, so a tenant may exceed its quota during an
   outage, and the overage is then not recorded. A tenant's current consumption lives only in the day's counter, and
   the durable history is the ledger of ADR-1199.

## Options considered

- **A rolling 24-hour window.** Rejected: it needs many buckets per tenant or an estimate over a day, and its reset
  time differs for every tenant, which a plan cannot state.
- **The tenant's own time zone.** Rejected: the boundary would differ per tenant, and a tenant that changes its time
  zone could reset its count.
- **Counting issued tokens rather than token requests.** Rejected: it needs a non-atomic read-then-increment, so
  replicas overshoot the quota under load.
- **A plan-tier setting scope.** Rejected: one tenant has one tier, so the scope would duplicate the tenant scope, and
  limits are written by the authority above their scope (ADR-1418).
- **A policy setting (`refuse` or `admit`) plus a separate ceiling setting.** Rejected: two settings per quota can
  disagree (an `admit` policy with no ceiling is unbounded billing), and the allowance alone says whether to admit
  and how far.
- **Unbounded overage for an admitting tier.** Rejected: an unbounded bill is a risk to the customer, and a runaway or
  compromised client would turn it into a denial of wallet. The per-minute limits bound the rate, not the day's total.
- **A percentage of the quota as the allowance.** Rejected: the overage would change when the quota changes, and a
  plan states its overage as a count as easily as a percentage.
- **Deriving the overage from the ledger (`requests` minus the quota).** Rejected: the ledger lags by a flush interval
  and is written per replica, while the quota is exact; recording the overage when it is admitted keeps the bill equal
  to what was served past the quota.
- **A dedicated overage endpoint.** Rejected: the usage report already lists every meter, and a billing system reads
  one shape for all of them.
- **Chosen:** a UTC-day fixed window in the shared counter set, an opt-in allowance per quota that is the hard
  ceiling, and overage meters in the ledger.

## Consequences

- A tenant with a quota and no allowance is refused for the rest of the UTC day once it spends the quota; nothing is
  limited by default.
- A tier opts in per quota by setting its allowance; nothing is billed while no allowance is set. The bill is bounded:
  a tenant is never served more than the quota plus the allowance in a UTC day, except during a shared-state outage.
- Anonymous traffic to a metered endpoint spends the request quota, as it spends the tenant's per-minute limit; the
  per-address limit bounds how much of it one address can spend. Anonymous traffic to discovery and the JSON Web Key
  Set spends only the per-minute limits. The token-request quota cannot be spent without a confidential client's
  credentials.
- The overage is reported with up to one flush interval of lag, like every ledger meter.
- A deployment that runs several replicas must configure Valkey for quotas to hold, as for rate limits.

## References

- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md) — unset means unrestricted
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — metering is endpoint metadata
- [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md) — the usage ledger
- [ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md) — rate limits and the counter set
- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) — who writes a limit
- [ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md) — customer environments as tenants
- `src/NCode.Identity.OpenId.Authentication/RateLimiting/DefaultRequestRateLimiter.cs`,
  `src/NCode.Identity.OpenId.Authentication/RateLimiting/DefaultRateLimitSettingDescriptorDataSource.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/DefaultTokenEndpointProvider.cs`,
  `src/NCode.Identity.OpenId.Authentication/Usage/TenantUsageMeters.cs`
