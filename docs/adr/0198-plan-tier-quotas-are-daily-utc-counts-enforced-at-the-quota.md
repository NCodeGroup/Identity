# 198. Plan-tier quotas are daily UTC counts enforced at the quota

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

Per-minute rate limits ([ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)) protect
capacity from bursts, but they do not bound what a tenant consumes over a day. The hosted platform sells plan tiers,
and a tier is defined by how much a tenant may use: requests to its protocol endpoints and token requests. Customers
also run their dev and test environments as tenants on the production platform
([ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md)), so a load test that stays under the
per-minute limit for hours still has to stop somewhere.

A quota must hold across replicas, as a limit does, and the family already has the primitive: the shared, atomic
`IExpiringCounterSet` with its in-process default and Valkey adapter. Three questions were open: the window (a calendar
day in UTC, or a rolling 24 hours), what happens at the quota (refuse, or admit and report an overage), and how a plan
tier sets the quota through the settings cascade.

## Decision

**A quota is one fixed window per UTC calendar day, counted atomically in the shared counter set and enforced at the
quota with `429` and a `Retry-After` until midnight UTC; a plan tier sets it as tenant settings beneath the server's
ceiling.**

1. **The window is the UTC calendar day.** A tenant's count starts at midnight UTC and its counter expires at the next
   midnight. A calendar day is what a plan states and what an operator can explain ("resets at 00:00 UTC"), it costs
   one atomic increment per request, and it needs no estimate: bursts are the per-minute limits' job. A rolling
   24 hours would need either many buckets per tenant or ADR-0196's two-window estimate over a day, for no gain a
   customer can see. UTC, not the tenant's time zone, so every replica and every tenant agree on the boundary.
2. **Enforced at the quota.** A request over a quota is refused with `429 Too Many Requests`, the
   `temporarily_unavailable` error code, `Retry-After` in whole seconds until midnight UTC, and
   `Cache-Control: no-store` — the response a per-minute limit gives, so a client handles both the same way. Admitting
   and reporting an overage is a billing policy that needs a durable usage record; the platform's first need is that a
   tenant's consumption is bounded. Overage billing can be added on top of the usage record without changing the
   enforcement.
3. **Two quotas, both tenant settings.**
    - `quota_tenant_requests_per_day` counts every request the tenant's protocol endpoints serve. It is judged after
      the address and tenant per-minute limits, so a request a limit refuses does not spend the quota, and one address
      can spend no more of it than its own per-minute limit allows.
    - `quota_tenant_token_requests_per_day` counts token requests from confidential clients, judged at the token
      endpoint after client authentication (and its per-client limit) and before the grant is processed. A public
      client is named by its `client_id` alone, which anyone can read from an authorization URL, so counting it would
      let any caller spend the quota and refuse every client of the tenant until midnight — the reason ADR-0196 limits
      only confidential clients. A public client's token requests are bounded by the request quota instead. It counts token requests rather than issued tokens: the count is incremented before the request
      is judged, which is atomic across replicas, while counting only successful issuance would need a read before
      the grant and an increment after it, which lets concurrent replicas overshoot. A token request the grant
      handler then refuses has still cost the platform its work.

    Each is an integer of at least one with no default, merged with `Min`, and unset means unlimited
    ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)). They are not discoverable.

4. **A plan tier is a set of tenant settings.** The platform's provisioning writes a tier's quotas on the tenant through
   the management API; the server's settings are the platform's ceiling, and the `Min` merge means no tier can raise a
   tenant above it. A tier is not a setting scope of its own: a tenant has one tier, so a scope between the server and
   the tenant would hold one value per tenant, which the tenant scope already does. Moving a tenant to another tier
   rewrites its settings.
5. **The same counter, the same failure mode.** Quotas are counted with the rate limiter's keys (purpose `rate-limit`,
   the tenant, the partition `daily_requests` or `daily_token_requests`, and the day's start). A counter store that
   fails admits the request and logs a warning, as a limit does; each judgement is counted in
   `ncode.identity.rate_limiting.requests` under its partition
   ([ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md)).

## Options considered

- **A rolling 24-hour window.** Rejected: it needs per-tenant buckets or an estimate, and its reset time differs for
  every tenant, which a plan cannot state.
- **The tenant's own time zone.** Rejected: the boundary would differ per tenant, and a tenant that changes its time
  zone could reset its count.
- **Admit and report an overage.** Deferred: it is a billing policy over a durable usage record, which does not exist
  yet; enforcement bounds consumption today and does not prevent it later.
- **Counting issued tokens rather than token requests.** Rejected: it needs a non-atomic read-then-increment, so
  replicas overshoot the quota under load.
- **A plan-tier setting scope.** Rejected for now: one tenant has one tier, so the scope would duplicate the tenant
  scope. It becomes worth adding if tiers change in bulk often enough that rewriting tenants is a burden.

## Consequences

- A tenant with a quota is refused for the rest of the UTC day once it spends it; nothing is limited by default.
- Anonymous traffic spends the request quota, as it spends the tenant's per-minute limit; the per-address limit bounds
  how much of it one address can spend. The token-request quota cannot be spent without a confidential client's
  credentials.
- A tenant's current consumption is the day's counter, which lives only until midnight. A durable usage history for
  billing and overage reporting is tracked in [`BACKLOG.md`](../../BACKLOG.md) as `F81`.
- A deployment that runs several replicas must configure Valkey for quotas to hold, as for rate limits.
- A Valkey outage lets requests through uncounted, so a tenant may exceed its quota by the traffic served during the
  outage.

## References

- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — unset means unrestricted
- [ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) — rate limits and the counter set
- [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) — protocol metrics
- `src/NCode.Identity.OpenId.Authentication/RateLimiting/DefaultRequestRateLimiter.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/DefaultTokenEndpointProvider.cs`
