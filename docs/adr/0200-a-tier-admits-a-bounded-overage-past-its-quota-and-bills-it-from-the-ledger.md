# 200. A tier admits a bounded overage past its quota and bills it from the ledger

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

A daily quota ([ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md)) refuses a tenant for
the rest of the UTC day once it is spent. ADR-0198 deferred the alternative, admitting the overage and billing it,
until a durable usage record existed. The usage ledger of
[ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) is that record. Three questions remained: how
a plan tier opts in, whether an admitting tier still has a hard ceiling, and how the overage reaches the customer.

## Decision

1. **Each quota has an overage allowance setting.** `quota_tenant_requests_overage_per_day` and
   `quota_tenant_token_requests_overage_per_day` are the most requests (or confidential clients' token requests) a
   tenant is served past the matching quota in one UTC calendar day. They are tenant-scope integers of at least `0`,
   merged with `Min`, exactly as the quotas are: a server-scope value is the default allowance every tenant inherits
   and the cap a tenant-scope value cannot exceed, and a tier opts out of a server default by setting `0`.
2. **Setting the allowance is the opt-in, and its value is the hard ceiling.** An effective allowance that is unset or
   `0` refuses at the quota, as before. A tier that sets an allowance is served up to the quota plus the allowance and
   refused past both until midnight UTC. Unset means _none_ here, unlike the unrestricted default of
   [ADR-0010](0010-supported-settings-unset-means-unrestricted.md): an allowance creates a charge, and a missing
   setting must never bill a customer without limit. A tier that wants effectively unlimited overage sets a large
   value. An allowance without its quota does nothing.
3. **The quota's counter judges the overage.** The day's shared counter of ADR-0198 is compared with the quota and
   then with the quota plus the allowance, so the overage needs no second counter and stays exact across replicas. A
   request admitted past the quota is counted in the `ncode.identity.rate_limiting.requests` metric with the result
   `overage`.
4. **Overage is billed from the ledger as its own meter.** An admitted overage request is recorded under
   `requests_overage` or `token_requests_overage` in addition to `requests` or `token_requests`, so the plain meters
   stay the total served and the overage meters are exactly what is billable. A request the per-client limit or the
   token quota refuses after the request quota admitted it as overage is not recorded, so it is not billed; public
   metadata (discovery and the JSON Web Key Set) never spends the request quota, so anonymous callers cannot run up a
   tenant's overage.
5. **The customer sees the overage through the existing usage report.** `GET /api/tenants/{tenantId}/usage` returns the
   overage meters with the others, and the admin UI's Usage tab shows them as columns, because both list every meter
   recorded. No new endpoint is needed.

## Options considered

- **A policy setting (`refuse` or `admit`) plus a separate ceiling setting.** Rejected: two settings per quota can
  disagree (an `admit` policy with no ceiling is unbounded billing), and the allowance alone already says whether to
  admit and how far.
- **Unbounded overage for an admitting tier.** Rejected: an unbounded bill is a risk to the customer, and a runaway or
  compromised client would turn it into a denial of wallet. The per-minute rate limits bound the rate, not the day's
  total.
- **A percentage of the quota as the allowance.** Rejected: it changes the overage when the quota changes, and a plan
  states its overage as a count as easily as a percentage.
- **Deriving the overage from the ledger (`requests` minus the quota).** Rejected: the ledger lags by a flush interval
  and is written per replica, while the quota is exact; recording the overage when it is admitted keeps the bill equal
  to what was served past the quota.
- **A dedicated overage endpoint.** Rejected: the usage report already lists every meter, and a billing system reads
  one shape for all of them.

## Consequences

- A tier opts in per quota by setting its allowance; nothing changes while no allowance is set at the server or the
  tenant scope. An operator who sets a server-scope allowance grants it to every tenant with a quota, as a server-scope
  quota applies to every tenant.
- The bill is bounded: a tenant is never served more than the quota plus the allowance in a UTC day, except during a
  shared-state outage, when the quotas fail open as ADR-0198 describes and the overage is then not recorded.
- The overage is reported with up to one flush interval of lag, like every ledger meter.

## References

- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — unset means unrestricted
- [ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md) — daily quotas
- [ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) — the usage ledger
- `src/NCode.Identity.OpenId.Authentication/RateLimiting/DefaultRequestRateLimiter.cs`,
  `src/NCode.Identity.OpenId.Authentication/RateLimiting/DefaultRateLimitSettingDescriptorDataSource.cs`,
  `src/NCode.Identity.OpenId.Authentication/Usage/TenantUsageMeters.cs`
