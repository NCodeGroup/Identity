# 1463. A seeded tenant is materialized from the row its seed committed

- **Status:** Accepted (amends
  [ADR-1166](1166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md),
  [ADR-1454](1454-a-replica-that-may-have-missed-a-tenant-announcement-evicts-only-the-tenants-whose-row-changed.md))
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A tenant is seeded each time it is materialized
([ADR-1166](1166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md)). Seed contributors write the
tenant row: a seeded or rolled key adds secrets, and development pruning removes them. The store issues a new secrets
token for each of those writes, and the interceptor regenerates the row token on save
([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)).

The tenant a seed reads before its contributors run carries the tokens of the row before the seed. The row token is what
a replica compares after a missed announcement
([ADR-1454](1454-a-replica-that-may-have-missed-a-tenant-announcement-evicts-only-the-tenants-whose-row-changed.md)),
the settings and secrets tokens are what its polls compare, and the management API answers a created tenant with its row
token as the ETag.

## Decision

**`ITenantProvisioner.SeedAsync` returns the tenant row its seed committed.**

1. **The provisioner reads the committed row.** Once the seed has committed and its post-commit steps have run,
   `SeedAsync` reads the tenant in a fresh unit of work and returns that row. A tenant removed in between is answered
   `404`, as is a tenant that is missing before the seed.
2. **A workload seed brings up the root tenant without that read.** Nothing uses the root tenant's row there.
3. **The tenant factory materializes from the returned row.** The materialized tenant carries the row, settings and
   secrets tokens of its own seed.
4. **The management API's create answers with the returned row.** It does not read the tenant a second time.
5. **`TenantSeedContext.Tenant` is the seed's working copy.** A contributor that changes the tenant's settings or
   secrets updates it, so the contributors that follow see the change before the commit.

## Options considered

- **Read the tenant after the commit.** One indexed read per seed returns every token from one consistent read, and every
  caller gets the committed row. Chosen.
- **Carry the committed tokens onto the working copy.** The interceptor regenerates the row token during save, outside
  any store method, and the data contract is detached from the tracked entity. Rejected.
- **Each caller reads the tenant again.** Every caller repeats the read, and a caller that omits it materializes the
  pre-seed row. Rejected.
- **Materialize from the pre-seed row.** Each materialization that seeds keys is evicted once on a revalidation, and its
  secrets poll reads the secrets again for nothing. Rejected.

## Consequences

- A seed costs one more indexed read, including the seed after a settings change, which discards the row.
- A freshly materialized tenant is current on a revalidation unless its row changed after its seed.
- A row that another writer changes between the commit and the read is returned as read, and that writer's announcement
  evicts the tenant.
- A custom `ITenantProvisioner` returns the committed row.

## References

- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) — the interceptor manages concurrency tokens
- [ADR-1166](1166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md) — a tenant is provisioned
  through one pipeline under every strategy
- [ADR-1454](1454-a-replica-that-may-have-missed-a-tenant-announcement-evicts-only-the-tenants-whose-row-changed.md) — a
  replica that may have missed a tenant announcement evicts only the tenants whose row changed
- [#619](https://github.com/NCodeGroup/identity/issues/619)
