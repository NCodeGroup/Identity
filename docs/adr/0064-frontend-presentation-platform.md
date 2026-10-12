# 64. A single cohesive frontend presentation platform

- **Status:** Proposed
- **Date:** 2026-10-10
- **Deciders:** NCode Group

## Context

The family has, by design, been UI-free: the OpenID Provider authenticates and challenges through standard
ASP.NET Core schemes and expects the host to supply any interactive surface, and the Management API is a
pure REST surface ([`ClientApiEndpointHandler`](../../src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs),
[`TenantApiEndpointHandler`](../../src/NCode.Identity.OpenId.Management/Endpoints/Tenants/TenantApiEndpointHandler.cs),
and siblings). Several converging needs now require a real presentation layer:

- **Management (`api/...`).** Tenants, clients, resource servers/scopes, grants, secrets, and settings are
  only reachable by hand-driving REST. The global surface (`api/servers/...`) and the tenant-scoped surface
  (`api/tenants/{tenantId}/...`) sit on **different security boundaries** and must be browsed distinctly.
- **Interactive auth (`T2`/[ADR-0062](0062-opt-in-interactive-login-consent-ui.md)).** Conformance needs a
  browser-drivable login + consent surface over the existing cookie-challenge seam.
- **Consent (`F2`).** Durable consent will need a user-facing approval screen.
- **Playground & OIDC conformance.** A reference host and a scriptable OP surface.
- **Branding (future).** Customer-facing surfaces must be themeable/brandable per tenant.

Building these as independent one-off UIs would duplicate the design system, the API client, auth
bootstrapping, and build tooling, and would drift. The decision is to establish **one cohesive frontend
platform** that all of these surfaces share.

This is **greenfield** ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)): we default to the
best long-term structure rather than the smallest patch. The platform lives in the monorepo today; a repo
split is anticipated at GA, so package boundaries are drawn to make that extraction mechanical.

## Decision

**Establish a single TypeScript frontend monorepo platform — React + Material UI, Redux Toolkit + RTK
Query, built with Vite, orchestrated by pnpm workspaces + Turborepo — that all presentation surfaces
(Management, Auth/Consent/Login, Playground, OIDC conformance) share. Reusable concerns are shared
`@ncode/*` workspace packages; each surface is a thin app shell that composes them. The invariant is a
clean layering — stateless presentational components, stateful container wiring, and UI-free
services/logic — so every layer is independently testable.** Concretely:

- **Placement.** The platform lives under [`src/frontend/`](../../src/frontend/) next to the .NET projects.
  It is a self-contained pnpm workspace with its own toolchain; the .NET build (`.slnx`, `dod.ps1`,
  `Directory.Build.*`) only compiles `.csproj` projects and never sees it. `node_modules/`, `dist/`,
  `.turbo/`, and coverage output are git-ignored.
- **Toolchain.** pnpm workspaces + Turborepo (cached build/lint/test graph), Vite per app, Vitest +
  React Testing Library for unit tests, Storybook for component verification, TypeScript everywhere
  (`strict`), ESLint + Prettier. The package-manager version is pinned via the `packageManager` field
  and activated with **Corepack** (Node-built-in), so every machine and CI runner uses the exact same
  pnpm with no global install. The frontend toolchain stays **out of the hermetic `dod.ps1` inner
  loop**; it has its own `pnpm` scripts and (later) its own CI job.
- **Full-stack dev launch is health-ordered and per-app self-describing.** The `Aspire: Full stack (F5)`
  launch ([ADR-0071](0071-aspire-local-dev-orchestrator.md)) orchestrates the Playground API plus each app as
  one .NET Aspire resource graph, gating the UIs on the API being healthy so they cannot race it. Each app's
  **`dev` script still encodes its own serving model**: a **backend-served** bundle (served by the .NET host
  from static `dist`, e.g. `apps/auth` through the interactive-login endpoint) uses `vite build --watch` to keep
  `dist` fresh; a **standalone** bundle (e.g. `apps/mgmt`) uses `vite` for an HMR dev server. Adding a surface is
  one `AddViteApp`/`AddJavaScriptApp` line in the AppHost — no central app registry or per-app branching.
- **App topology — shared packages + thin app shells.** Independent deployable bundles per surface
  maximize reuse and preserve the security boundary between surfaces:
    - `@ncode/config` — runtime tier discovery (see below).
    - `@ncode/store` — Redux Toolkit store assembly, typed hooks, slice registration.
    - `@ncode/api` — the RTK Query client: typed endpoints generated/modeled from the Management REST
      contracts (`TenantResource`, `ClientResource`, `CollectionResource<T>`, settings/secrets), auth-token
      injection, tag-based cache invalidation.
    - `@ncode/ui` — the Material UI design system: theme, brandable tokens, and **stateless** presentational
      components with Storybook stories.
    - `apps/mgmt`, `apps/auth`, `apps/playground`, … — thin Vite shells that wire the packages.
- **Layering is the testability invariant.** Presentational components are **stateless** and prop-driven
  so they render in Storybook and under RTL without a store; local component state is allowed only when it
  is intrinsic to the control and does not break isolated rendering. State and side effects live in
  **container** components at the top of each screen that wire Redux + RTK Query. All non-trivial logic
  (validation, transforms, PKCE, discovery parsing, boundary rules) lives in **UI-free services/helpers**
  that are unit-tested directly. RTK Query is the default for server state; hand-rolled thunks are the
  exception.
- **Tier-agnostic bundle via runtime discovery.** A bundle is built **once** and runs unchanged in
  sandbox/test/production. **No** tier configuration is baked at build time. On boot, `@ncode/config`
  fetches a runtime discovery document from the serving host (a well-known bootstrap endpoint + the OP's
  `/.well-known/openid-configuration`) to learn the API base, issuer, auth endpoints, and feature flags,
  then hydrates the store before the app renders. The host that serves the static bundle owns the
  discovery document.
- **Global vs. tenant security boundary is first-class.** The Management surface models the global scope
  (`api/servers/...`) and tenant scope (`api/tenants/{tenantId}/...`) as **distinct browsing contexts**
  with distinct navigation, cache tags, and authorization gating — not one list with a filter — so the
  boundary is visible and enforced in the UI, not merely cosmetic.
- **Branding is a seam, not yet a feature.** `@ncode/ui` centralizes theme tokens behind a provider so a
  future per-tenant brand/theme can be injected via the same runtime-discovery channel. No branding
  feature ships now; the seam exists so it is additive later.
- **Monorepo now, clean split later.** Every shared concern is a scoped `@ncode/*` package consumed only
  through its public entry point — no deep relative imports across package boundaries — so `src/frontend/`
  can be extracted into its own repository at GA by moving the folder and repointing the workspace root,
  with no source rewrites.

## Options considered

- **Single unified SPA with route-gated surfaces.** One bundle, simpler deploy, but a weaker security
  boundary (all surfaces in one origin/bundle), a larger blast radius, and coupling between the
  management console and the end-user login surface. Rejected in favor of per-surface bundles sharing
  packages.
- **Separate one-off UIs per need.** Fastest for the first screen, but duplicates the design system, API
  client, auth bootstrap, and tooling, and guarantees drift. Rejected.
- **Build-time tier configuration (per-environment bundles).** Simple, but forfeits the
  "one bundle, every tier" requirement and invites config/bundle mismatch. Rejected for runtime
  discovery.
- **Nx vs. pnpm + Turborepo.** Nx is more opinionated with a heavier generator/plugin surface; pnpm
  workspaces + Turborepo is lighter, disk-efficient, and sufficient for a package+apps graph. Chose the
  lighter stack.
- **Package-manager modernity — pnpm + Corepack vs. Yarn PnP / zero-installs.** pnpm's strict,
  content-addressable, symlinked `node_modules` plus Corepack version-pinning gives reproducible,
  fast, strict installs that the whole ecosystem understands. Yarn Berry's Plug'n'Play (no
  `node_modules`, a `.pnp.cjs` resolver) and zero-installs (committing the dependency cache to git)
  are the strictest/fastest options in theory, but PnP still needs patches/unplugged packages for the
  Vite + Storybook + emotion/MUI stack, and zero-installs bloats the repo with binary archives —
  doubly undesirable given the anticipated GA repo split. Rejected PnP/zero-installs; pnpm's symlinked
  layout captures most of the strictness benefit without the compatibility tax. (pnpm's own
  experimental PnP mode is likewise left off for the same reason.)
- **Placement `src/frontend/` vs. top-level `web/`.** `web/` keeps the toolchains visually separate, but
  `src/frontend/` keeps all source under one root and the .NET build already ignores non-`.csproj`
  content. Chose `src/frontend/` with clean package boundaries so a future repo split is still mechanical.
- **Redux Toolkit vs. React Query/Zustand.** RTK + RTK Query gives one cohesive state model (server +
  client state, cache tags, devtools) and matches the stateless/stateful layering. Chosen.

## Consequences

- The family gains its first UI and a new toolchain (Node/pnpm) in the repo. It is isolated under
  `src/frontend/` and does not affect the .NET Definition of Done; it needs its own lint/test/build CI
  job, added separately.
- All future surfaces (`T2` login/consent, `F2` consent, Playground, conformance) build on this platform
  instead of re-inventing a UI, and inherit the design system, API client, auth bootstrap, and discovery.
- The stateless/stateful/service layering becomes a maintained convention (captured in
  [`src/frontend/README.md`](../../src/frontend/README.md)); breaking it (state in presentational
  components, logic in components) is a review failure because it breaks Storybook/unit isolation.
- The runtime-discovery contract becomes a coupling between the serving host and the bundle: the host
  must publish a discovery document the bundle understands. The management REST contracts become a typed
  dependency of `@ncode/api`; contract changes ripple into the client types (intended — it keeps them in
  sync).
- The anticipated GA repo split is pre-paid: clean `@ncode/*` boundaries mean extraction is a folder move,
  not a refactor.

## References

- Backlog: [`BACKLOG.md`](../../BACKLOG.md) — interactive UI (`T2`), consent (`F2`), presentation
  platform (new group `U`).
- Related: [ADR-0062](0062-opt-in-interactive-login-consent-ui.md) (opt-in login/consent UI and its
  stable-locator contract — the Auth surface of this platform must honor it),
  [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) (greenfield posture),
  [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) (service/strategy/mediator model the
  API this UI drives is built on).
- Management REST surface:
  [`TenantApiEndpointHandler`](../../src/NCode.Identity.OpenId.Management/Endpoints/Tenants/TenantApiEndpointHandler.cs),
  [`ClientApiEndpointHandler`](../../src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs),
  [`ServerApiEndpointHandler`](../../src/NCode.Identity.OpenId.Management/Endpoints/Servers/ServerApiEndpointHandler.cs)
  (global scope).
- Platform guide: [`src/frontend/README.md`](../../src/frontend/README.md).
  </content>
  </invoke>
