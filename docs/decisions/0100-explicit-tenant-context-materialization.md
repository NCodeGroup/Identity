# 100. Explicit-tenant OpenID context materialization for admin operations

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

The management/admin surface is addressed by a route `{tenantId}` (`/api/tenants/{tenantId}/…`). A tenant-bound
endpoint opens an **ambient tenant scope** from that route segment (`IAmbientTenantAccessor.BeginScope(tenantId)`), which
the persistence-layer query filter reads to confine every read to the addressed tenant — fail-closed, and independent of
the request's own resolved tenant ([ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md)).
That ambient scope is a **bare string**: enough to isolate data reads, but not a materialized `OpenIdTenant` (settings,
secrets, issuer).

That gap blocked the admin **sessions** surface ([#63](https://github.com/NCodeGroup/Identity/issues/63),
[ADR-0098](0098-session-lifecycle-termination-seam.md)). `ISessionService` is bound to an `OpenIdContext` — it keys the
session grant on `OpenIdContext.Tenant.TenantId` and dispatches the termination seam through `OpenIdContext.Mediator`,
and that seam's **back-channel logout** handler must mint and sign a logout token with the **addressed tenant's** issuer
and signing keys. The management request's ambient `OpenIdContext` carries the request-resolved tenant (the management
host), not the route-addressed one, so delegating to `ISessionService` with it would key grants and sign logout tokens
in the **wrong** tenant. The interim grants surface sidesteps this only by bypassing the seam entirely (a direct
`store.RevokeWhereAsync`), which is why it sends no back-channel logout and runs no session audit.

Two shapes were considered: (a) give `ISessionService` tenant-parameterized overloads, or (b) materialize a full
`OpenIdContext` for the addressed tenant and keep `ISessionService` context-bound. Option (a) collapses into (b) anyway —
the termination seam still needs a materialized tenant to sign the logout token — while also forking the service surface.

## Decision

**Add a first-class capability to materialize an `OpenIdTenant` / `OpenIdContext` for an explicit, route-addressed
tenant, decoupled from the request's tenant-resolution strategy; keep `ISessionService` context-bound and have the admin
endpoint delegate to it through that materialized context.**

- `IOpenIdTenantFactory` gains `CreateTenantByIdAsync(httpContext, env, server, tenantId, …)`, alongside the renamed
  request path `CreateTenantForRequestAsync(…)` (the method pair is now self-documenting; "resolve" stays reserved for
  `ITenantResolver` selecting a `PersistedTenant`). The by-id path loads the `PersistedTenant` directly from
  `ITenantStore` and reuses the same materialization helpers (settings, secrets).
- `IOpenIdContextFactory` gains `CreateForTenantAsync(httpContext, tenantId, mediator, …)`, alongside the renamed
  request path `CreateForRequestAsync(…)`. The returned context is **caller-owned and disposable**, and is deliberately
  **not** published as the ambient `OpenIdContext` request feature — it is a secondary context for the addressed tenant,
  distinct from the request's own.
- The by-id materialization is **request-independent and cache-isolated**:
    - The tenant's **own configured issuer** (`TenantIssuer` setting) is the authoritative identity and the base
      address; there is no inbound request for this tenant from which to derive a host. A tenant with **no configured
      issuer cannot be materialized this way** (it would otherwise vary by request host) — the factory fails fast.
    - It does **not** read or write the shared tenant cache. The cache holds request-materialized tenants (base address
      derived from the request host); aliasing an issuer-derived admin materialization with it, in either direction,
      would be a correctness hazard. Admin session operations are infrequent, so materializing fresh is an acceptable
      cost for full isolation.
- The admin sessions endpoint (`/api/tenants/{tenantId}/sessions`) uses this: a **read** (list) runs on the request's
  ambient context (the session query is constrained by the route's ambient tenant scope and needs only the environment),
  while a **revoke** materializes the addressed-tenant context via `CreateForTenantAsync` and delegates to
  `ISessionService.RevokeAsync` / `RevokeAllAsync`, so the full termination seam (grant revocation, back-channel logout,
  and session auditing) runs in the addressed tenant's security domain. The admin action is additionally attributed to
  the acting administrator via `IManagementAuditRecorder.RecordSessionsRevokedAsync` (`session.revoked.admin`).

## Consequences

- The admin sessions surface runs the same correctness path as a protocol logout — unlike bulk grant revocation, an
  admin session revocation notifies relying parties and audits per session — closing the gap called out in
  [ADR-0098](0098-session-lifecycle-termination-seam.md).
- The capability is general: any future management operation that must drive protocol machinery for a route-addressed
  tenant (not just sessions) can materialize that tenant's context the same way.
- A tenant **without a configured issuer** cannot be the target of an admin operation that requires a materialized
  context. That is the correct constraint — such a tenant has no request-independent identity to sign under — and it
  reinforces configuring `TenantIssuer` explicitly rather than relying on request-host derivation.
- `ISessionService` stays context-bound (its correct shape); no tenant-parameterized overloads were added.

## References

- [ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md) — tenant resolution as a shared
  abstraction and the management boundary.
- [ADR-0036](0036-unified-openid-request-environment.md), [ADR-0037](0037-tenant-cache-probe-before-store-load.md) — the
  request-driven tenant materialization and cache this capability deliberately parallels but does not share.
- [ADR-0098](0098-session-lifecycle-termination-seam.md) — the session-termination seam the admin surface drives, and
  the deferral this decision resolves.
