# 32. Tenant-scoped rows carry a denormalized tenant key, so tenant-scoping needs no join to the tenant table

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

[ADR-0024](0024-control-plane-and-per-tenant-planes.md) sets the target of isolating a tenant's data into its own
database and lays out the realization as phased work: (1) a control/tenant split, (2) per-tenant connection routing,
(3) cross-plane reads at materialization. The single-database deployment remains the valid default, and
[ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) scopes tenant-bound data with a global query
filter _within_ that one database.

Those query filters, however, scoped each tenant-child row by **navigating to the tenant table**:
`entity.Tenant.NormalizedTenantId == NormalizedAmbientTenantId`. That join is the obstacle to ever moving a tenant's
rows to a separate database: the tenant registry (the `Tenants` table) is control-plane data, so a per-tenant database
would not contain it, and a scoping predicate that joins to it could not run there. A tenant-scoped row was therefore
**not self-identifying** — it only knew its tenant through a foreign key into a table that the split would relocate.

Separately, `GrantEntity` carried a `TenantId` foreign key and a `Tenant` navigation but — unlike every other
tenant-scoped entity — did not implement `ISupportTenantEntity`, so it was excluded from the shared tenant-entity
contract.

## Decision

**Every tenant-scoped entity carries a denormalized `NormalizedTenantId` — the normalized natural tenant id stored on
the row itself — and the tenant-scoping query filters read it directly instead of joining to the tenant table. This is
the first, data-model step of the ADR-0024 control/tenant split.**

- **`ISupportTenantEntity` gains `string NormalizedTenantId`** alongside the existing `TenantId` foreign key and
  `Tenant` navigation. `GrantEntity` now implements `ISupportTenantEntity`, bringing it in line with the other
  tenant-scoped entities.
- **Each tenant-scoped entity** (`ClientEntity`, `ClientSecretEntity`, `ResourceServerEntity`, `ScopeEntity`,
  `ClientGrantEntity`, `GrantEntity`, `TenantSecretEntity`) persists `NormalizedTenantId`, written by the stores at
  insert time from the resolved tenant (the stores already hold the tenant to assign the foreign key), and indexed for
  the scoping predicate.
- **The global query filters read the denormalized key** — `entity.NormalizedTenantId == NormalizedAmbientTenantId` —
  so a tenant-scoped surface sees only its tenant's rows and a central-admin (unset ambient tenant) surface sees all,
  exactly as before, but **without a join to the tenant table**. The row is now self-identifying.
- **The foreign key and navigation are retained.** The denormalized key is additive: referential integrity and the
  `Tenant` navigation remain for the single-database deployment, which is still the default.

The **physical** split — distinct control and per-tenant `DbContext` types, per-tenant connection routing, and
cross-plane reads at materialization (ADR-0024 phases 2–3) — is intentionally **not** done here. It depends on a
connection-routing design decision (how the ambient tenant selects a database) that is out of scope for this
data-model change; this ADR delivers the prerequisite that makes that later split additive rather than a redesign.

## Options considered

- **Denormalize the tenant key onto each row and filter on it, keeping the FK/navigation (chosen).** Makes every
  tenant-scoped row self-identifying and removes the cross-plane join from the hot scoping path, while leaving the
  single-database deployment unchanged. Purely additive.
- **Keep joining to the tenant table.** Rejected: it hard-wires every tenant-scoped query to the control-plane
  registry, which a per-tenant database cannot contain — the exact dependency the isolation goal must break.
- **Drop the tenant foreign key now and rely solely on the denormalized key.** Rejected as premature: it would weaken
  referential integrity in the shared-database default before the physical split exists to justify it. The FK stays
  until the connection-routing split actually relocates the rows.
- **Do the full control/tenant `DbContext` split now.** Deferred: without a connection-routing decision it would be a
  structural change with no validated behavior, and it is cleanly additive on top of this denormalization once that
  decision is made.

## Consequences

- Tenant-scoping no longer joins to the `Tenants` table; the scoping predicate is a single indexed column comparison on
  the row itself, which is both faster and relocatable.
- A per-tenant database becomes feasible without carrying the tenant registry: its rows scope themselves by their
  denormalized tenant key. The remaining work to physically split contexts and route connections is additive.
- `GrantEntity` now participates in the `ISupportTenantEntity` contract, removing a long-standing inconsistency.
- The change is schema-affecting (a new column and index per tenant-scoped entity) but, in this greenfield pre-release
  (no maintained migrations, the database is recreated), requires no migration.

## References

- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — the control-plane / per-tenant-plane model whose phase-1
  data-model prerequisite this change delivers.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the tenant-scoping query filter whose
  predicate this change rewrites to use the denormalized key.
