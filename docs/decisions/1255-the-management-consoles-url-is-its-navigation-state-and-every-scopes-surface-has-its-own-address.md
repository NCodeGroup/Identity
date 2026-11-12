# 1255. The management console's URL is its navigation state, and every scope's surface has its own address

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The management console (`apps/mgmt`) is a single-page app that serves a platform, a team and a tenant. A view that
lives only in React state cannot be bookmarked, linked or restored on reload, and the browser's back button leaves the
console. Auth0's dashboard addresses a tenant in its route (`manage.auth0.com/dashboard/{region}/{tenant}/…`), and
tenant identifiers are readable names
([ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)), so they fit in a
path.

[ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md) makes the
platform, team and tenant boundary a first-class browsing context, and the route has to keep it visible. The views of a
tenant itself, its secrets, encryption keys, settings, access and usage, are as much surfaces of the tenant as its
clients are, so each needs an address of its own rather than a tab held in state beneath one surface.

## Decision

**The address bar is the console's only navigation state, and every surface of every scope has its own address.**
`apps/mgmt/src/routes.ts` defines the route grammar as UI-free, unit-tested functions, and `navigation.ts` binds it to
the browser's history: `navigateTo` pushes a route's path (or replaces it), and `useConsoleRoute` reads
`location.pathname` through `useSyncExternalStore` and re-renders on `popstate`. Back, forward and reload land on the
same scope and surface.

| Path                              | Scope    | Surfaces                                                                                          |
| --------------------------------- | -------- | ------------------------------------------------------------------------------------------------- |
| `/home`                           | caller   | the places the caller holds authority                                                             |
| `/teams`, `/clients`, `/settings`, `/encryption-keys` | platform | `PLATFORM_SURFACES`                                                          |
| `/teams/{teamId}/{surface}`       | team     | `tenants`, `teams`, `clients`, `settings`, `access`, `usage` (`TEAM_SURFACES`)                    |
| `/tenants/{tenantId}/{surface}`   | tenant   | `clients`, `resource-servers`, `grants`, `secrets`, `encryption-keys`, `settings`, `access`, `usage` (`TENANT_SURFACES`) |

- **`/home` is the caller's home.** It shows the places the caller holds authority
  ([ADR-1465](1465-role-lists-share-one-audited-grant-and-revoke-flow-and-a-caller-discovers-its-own-assignments.md)),
  and it is where a path that names no route lands (`DEFAULT_ROUTE`).
- **A scope without a surface opens its first.** A team path without a surface opens `tenants`, and a tenant path
  without a surface opens `clients`.
- **The surface selects what a page renders.** `TenantOwnSurface` (`secrets`, `encryption-keys`, `settings`, `access`
  and `usage`) is the type of the tenant surfaces that show the tenant itself rather than the resources it holds.
  `TenantDetailPage` takes a `surface` prop of that type, renders that surface and holds no tab state, so each tab change
  is a history entry.
- **The scope is derived from the route, never held separately.** `routeScope(route)` yields the `ManagementScope`, so
  the containers cannot disagree with the address bar, and every address shows the platform, team or tenant boundary.
- **A team or tenant id is one percent-encoded segment.** `encodeURIComponent` on format and `decodeURIComponent` on
  parse round-trip any name. A blank id or a malformed encoding names no route.
- **Non-canonical paths are rewritten in place.** `resolveRoute` maps any path to a route and its canonical path, which
  `routePath` always builds, so the result is a same-origin console path. `useConsoleRoute` rewrites a non-canonical
  path (a trailing slash, a missing surface, an unknown path) with `history.replaceState`, so back never returns to it.
- **A deep link survives sign-in.** The sign-in screen sends `/bff/login?returnUrl=<path>`, and the returned path goes
  through `resolveRoute`, so whatever was sent, the round trip cannot become an open redirect.
- **Scope surfaces are keyed by their id.** Moving between teams or tenants through history remounts them, so no open
  detail view crosses the boundary. The usage surface's date range is local state, not part of the address, so a link
  opens the default range.
- **No router dependency.** A parse/format pair plus a history binding covers the grammar, and every rule is
  unit-tested. A nested route would extend `ConsoleRoute` and its grammar. The console's host answers every non-file
  path with `index.html`
  ([ADR-1438](1438-the-management-console-holds-no-token-and-reads-its-session-from-its-bff.md)).

## Options considered

- **Nested `/tenants/{tenantId}/settings/{tab}`.** Rejected: it adds a second row of tabs for a flat set of peers, and a
  depth to the grammar that no other surface has. A tenant's secrets, settings and usage are surfaces of the tenant, as
  its clients are.
- **Keep the tabs, and the active scope, in React state.** Rejected: the views cannot be linked, reloaded or reached
  with back and forward.
- **A router library.** Rejected: it adds a dependency and a second vocabulary for rules a short, unit-tested
  parse/format pair already covers.
- **The address bar as the only navigation state, with a surface per address.** Chosen.

## Consequences

- Every view of a platform, a team and a tenant can be bookmarked, shared and reloaded, and back and forward move between
  surfaces and scopes.
- After sign-in the console returns to the page the sign-in started from, rather than to the origin.
- The route grammar is covered by unit tests of `routes.ts`, with no UI in the way.
- The usage surface's date range is not addressable.

## References

- [ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md): a single
  cohesive frontend presentation platform.
- [ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md): tenant identifiers
  are readable names.
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md): the
  platform, its teams and its tenants.
- [ADR-1465](1465-role-lists-share-one-audited-grant-and-revoke-flow-and-a-caller-discovers-its-own-assignments.md): a
  caller discovers its own assignments.
- [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md): tenant usage is a
  daily ledger.
- [ADR-1438](1438-the-management-console-holds-no-token-and-reads-its-session-from-its-bff.md): the console reads its
  session from its BFF and its host answers every console path with `index.html`.
- Code: `frontend/apps/mgmt/src/routes.ts`, `frontend/apps/mgmt/src/navigation.ts`, `frontend/apps/mgmt/src/App.tsx` and
  `frontend/apps/mgmt/src/containers/TenantDetailPage.tsx`.
