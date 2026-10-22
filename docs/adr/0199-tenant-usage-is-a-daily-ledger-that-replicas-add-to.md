# 199. Tenant usage is a daily ledger that replicas add to

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

The daily quotas of [ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md) keep only the
current UTC day's count, in a shared counter that expires at midnight. The hosted platform needs more: it bills a plan
on what a tenant consumed over a period, reports that usage to the customer, and may later admit and bill an overage
instead of refusing at the quota. That needs a durable record of each tenant's usage, with its own retention, that the
management API can read.

Usage must stay out of metric series: a tenant attribute on a metric creates a series per tenant
([ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) §6). Two sources were
proposed: rolling the day's quota counter up into storage, or appending a record per request through the event seam.

## Decision

**Usage is a ledger in the OpenID database with one row per tenant, UTC day, and meter. Each replica counts usage in
memory and adds it to the ledger every minute, in batches that are each one unit of work. The ledger keeps 400 days by
default and the management API reads it.**

1. **One row per tenant, UTC day, and meter.** The `TenantUsage` table holds the tenant's id, the day, the meter, and
   the count, unique on the three keys. The day is the UTC calendar day, the window the quotas count. The meter is an
   open vocabulary ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)): the family records `requests`
   and `token_requests` (`TenantUsageMeters`), and a package records its own through `ITenantUsageRecorder.Record`,
   which refuses a name the ledger cannot store, so a bad name fails at its caller rather than at every flush. A
   row references its tenant by id, with no foreign key and no tenant query filter, so the usage of a deleted tenant
   survives for its final bill.
2. **Every tenant is metered, with or without a quota.** A request to a metered endpoint is counted once its endpoint
   has run, provided no limit refused it: neither the per-minute limits and the daily request quota
   (`RateLimitEndpointFilter`) nor the per-client limit or the token quota inside the endpoint, which mark the request
   refused when they answer it `429`. A token request is counted once a confidential client's token request is
   admitted by the token quota. Public clients do not count as token requests, as they do not spend the quota. A
   refused request is not usage, and neither is a request for public metadata (discovery and the JSON Web Key Set,
   [ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md)).
3. **Replicas count in memory and add to the ledger.** `ITenantUsageRecorder.Record` increments an in-memory count
   under a lock and costs no I/O on the request path. `TenantUsageFlushService` drains the counts every
   `TenantUsageOptions.FlushInterval` (one minute) and adds them in batches of 256 rows, each read with one query per
   day and saved as one unit of work, so the time between a batch's read and its save stays short however many tenants
   are active. The row's concurrency token serializes replicas that add to the same row; this works on every
   provider, including ones without bulk updates. A failed save — a conflict with another replica or an unreachable
   database — returns that batch's counts to memory and the next flush adds them. A graceful stop flushes once more, so
   an abrupt stop loses at most one interval of one replica's usage.
4. **The ledger keeps 400 days by default.** That covers a full year with a margin for a late invoice or dispute.
   `TenantUsageOptions.Retention` changes it. Each replica removes the days past retention once per UTC day; the
   removal is idempotent, so replicas need not coordinate.
5. **The management API reads the ledger.** `GET /api/tenants/{tenantId}/usage?from=&to=` returns the tenant's rows
   for an inclusive range of UTC days, ordered by day and meter. The range defaults to the last 30 days and spans at
   most 366. The endpoint authorizes a read of the tenant's node, as reading the tenant itself does.

## Options considered

- **Roll the day's quota counter up into storage.** Rejected: the counter exists only for a tenant with a quota,
  counts refused requests as well as admitted ones, and expires at midnight, so a rollup races its expiry and misses
  every tenant without a quota.
- **Append a record per request through the event seam.** Rejected: a row per request is log-level write load on the
  database and on every event sink, the load for which ADR-0197 also declines to publish rate-limit refusals as events.
- **Increment rows with a bulk update statement.** Rejected: it is not supported by every Entity Framework provider
  the family runs on, and a concurrency token gives the same correctness with a retry.
- **Store usage in the shared-state store (Valkey).** Rejected: shared state is for short-lived coordination and may
  be evicted; billing data belongs with the tenant's other durable records.

## Consequences

- A request costs one in-memory increment per meter; the database sees one small unit of work per replica per minute.
- The ledger can lag by up to one flush interval, and an abrupt stop of a replica loses up to one interval of its
  usage. Billing tolerates both; a quota, which must be exact, keeps using the shared counter.
- Overage billing — admitting a request past the quota and billing it — is built on the ledger: the admitted overage
  is recorded under its own meters
  ([ADR-0200](0200-a-tier-admits-a-bounded-overage-past-its-quota-and-bills-it-from-the-ledger.md)).
- The ledger is not exposed as a metric; dashboards that need per-tenant usage read the management API. The admin UI
  shows it on the tenant page's Usage tab, one column per meter present, so a new meter appears without a UI change.

## References

- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) — protocol metrics
- [ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md) — daily quotas
- `src/NCode.Identity.OpenId.Authentication/Usage/`,
  `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Stores/TenantUsageStore.cs`,
  `src/NCode.Identity.OpenId.Management/Endpoints/Tenants/TenantApiEndpointHandler.cs`
