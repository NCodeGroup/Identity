# 255. Each tenant surface of the management console has its own address

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

[ADR-0174](0174-the-management-consoles-url-is-its-navigation-state.md) makes the address bar the console's navigation
state, so a tenant's `clients`, `resource-servers`, `grants` and `settings` surfaces are each linkable, reloadable, and
reachable with back and forward. The tenant's own secrets, settings and usage are not: they are tabs held in React
state beneath the one `settings` surface. A link to the tenant's usage opens the secrets, a reload returns to the first
tab, and back returns to the previous surface instead of the previous tab. The "Settings" tab also opens on the
secrets, so its label names one of the three views it holds (backlog `R45`).

## Decision

**The tenant's own secrets, settings and usage are tenant surfaces, each with its own address and its own console
tab.**

| Path                                   | Tab              | Shows                                         |
| -------------------------------------- | ---------------- | --------------------------------------------- |
| `/tenants/{tenantId}/clients`          | Clients          | the tenant's clients                          |
| `/tenants/{tenantId}/resource-servers` | Resource servers | the tenant's resource servers                 |
| `/tenants/{tenantId}/grants`           | Grants           | the tenant's grants                           |
| `/tenants/{tenantId}/secrets`          | Secrets          | the tenant's own secrets                      |
| `/tenants/{tenantId}/settings`         | Settings         | the tenant's own settings                     |
| `/tenants/{tenantId}/usage`            | Usage            | the tenant's daily usage ledger for a range   |

- **The route grammar lists six tenant surfaces.** `TENANT_SURFACES` in `apps/mgmt/src/routes.ts` is `clients`,
  `resource-servers`, `grants`, `secrets`, `settings` and `usage`, in tab order. `TenantOwnSurface` is the type of the
  three that show the tenant itself rather than the resources it holds: `secrets`, `settings` and `usage`.
- **The address selects the surface.** `TenantDetailPage` takes a `surface` prop of type `TenantOwnSurface`, renders
  that surface, and holds no tab state. `App` renders it for those three surfaces and maps a tab change to
  `navigateTo`, so each tab is a history entry.
- **A tenant without a surface opens on `clients`.** `/tenants/{tenantId}` resolves to `/tenants/{tenantId}/clients`,
  and every other rule of ADR-0174 applies to the three surfaces unchanged: the scope derives from the route, the
  tenant id is one percent-encoded segment, a non-canonical path is rewritten in place, and a deep link survives
  sign-in.

## Options considered

- **Nested `/tenants/{tenantId}/settings/{tab}`.** Rejected: it adds a second row of tabs for a flat set of three
  peers, and a depth to the grammar that no other surface has. A tenant's secrets, settings and usage are as much
  surfaces of the tenant as its clients are.
- **Keep the tabs in React state.** Rejected: the views cannot be linked, reloaded or reached with back and forward,
  which is the contract of ADR-0174.

## Consequences

- Every tenant view is addressable.
- The grammar has two scopes and eight surfaces: `tenants` and `servers` in the global scope, and the six tenant
  surfaces above. `settings` addresses only the tenant's settings.
- The usage surface's date range is local state, not part of the address, so a link opens the default range.

## References

- [ADR-0174](0174-the-management-consoles-url-is-its-navigation-state.md): the management console's URL is its
  navigation state.
- [ADR-0064](0064-frontend-presentation-platform.md): a single cohesive frontend presentation platform.
- [ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md): tenant usage is a daily ledger that replicas
  add to.
- Code: `src/frontend/apps/mgmt/src/routes.ts`, `src/frontend/apps/mgmt/src/App.tsx` and
  `src/frontend/apps/mgmt/src/containers/TenantDetailPage.tsx`.
