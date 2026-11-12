# 1174. The management console's URL is its navigation state

- **Status:** Accepted (amended by
  [ADR-1255](1255-each-tenant-surface-of-the-management-console-has-its-own-address.md):
  the tenant's own secrets, settings, and usage are separate surfaces; amended by
  [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md): the console addresses a
  team and its tenants)
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The management console (`apps/mgmt`) kept its active scope (global or one tenant) and surface in React state. A tenant
view could not be bookmarked, linked, or restored on reload, and the browser's back button left the console. Auth0's
dashboard addresses a tenant in its route (`manage.auth0.com/dashboard/{region}/{tenant}/…`), and tenant identifiers are
readable names ([ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)), so they fit in a path.

[ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md) makes the global-versus-tenant boundary a first-class browsing
context, and the route has to keep it visible.

## Decision

**The address bar is the single source of the console's navigation state.** `apps/mgmt/src/routes.ts` defines the route
grammar as UI-free, unit-tested functions:

| Path                             | Scope  | Surfaces                                           |
| -------------------------------- | ------ | -------------------------------------------------- |
| `/tenants`, `/servers`           | global | the tenant list, the server                        |
| `/tenants/{tenantId}/{surface}`  | tenant | `clients`, `resource-servers`, `grants`, `settings` |

- **The scope is derived from the route, never held separately.** `routeScope(route)` yields the `ManagementScope`, so
  the containers cannot disagree with the address bar. A global path has no tenant segment, and every tenant surface is
  nested under its tenant, so the boundary shows in every address.
- **The tenant id is one percent-encoded segment.** `encodeURIComponent` on format and `decodeURIComponent` on parse
  round-trip any tenant name. A blank id or a malformed encoding names no route.
- **Non-canonical paths are rewritten in place.** `resolveRoute` maps any path to a route and its canonical path. The
  bare origin and unknown paths resolve to `/tenants`, a trailing slash is dropped, and a tenant without a surface opens
  on `clients`. The console replaces the history entry rather than pushing one, so back never returns to the
  non-canonical form.
- **Each navigation is a history entry.** `navigation.ts` binds the grammar to the browser: `navigateTo` pushes the
  route's path, and `useConsoleRoute` reads `location.pathname` through `useSyncExternalStore` and re-renders on
  `popstate`. Back, forward, and reload all land on the same scope and surface. The tenant surfaces are keyed by tenant,
  so moving between tenants through history remounts them, and no open detail view crosses the boundary.
- **A deep link survives sign-in.** Once the `/authorize` URL has been built, `AuthGate` remembers the current path in
  session storage. After the callback, it restores the path before the console mounts. The restore goes through
  `resolveRoute`, so whatever was stored, the result is a same-origin console path, and the round trip cannot be turned
  into an open redirect.
- **No router dependency.** The grammar has two scopes and six surfaces. A parse/format pair plus a history binding
  covers it in about a hundred lines, and every rule is unit-tested. A router library would add a dependency and a second
  vocabulary for the same rules. If the console grows nested routes, for example a client detail view, they extend
  `ConsoleRoute` and its grammar.

The console is served from the origin root (`base: '/'`, [ADR-1151](1151-every-frontend-workspace-builds-and-tests-from-one-preset-that-fails-on-warnings-and-oversized-chunks.md)).
Its host must answer every console path with `index.html`. The Vite dev server and `vite preview` already do this, and a
production static host needs the same SPA fallback.

## Consequences

- A tenant view can be bookmarked, shared, and reloaded, and back and forward move between surfaces and scopes.
- The tenant's own secrets and settings surface is addressed as `settings`, and its tab reads "Settings".
- After sign-in the console returns to the page the sign-in started from, rather than to the origin.
- A production deployment of the console needs SPA fallback routing on its static host.
