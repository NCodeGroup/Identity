# 1064. One frontend platform of shared packages and thin app shells serves every web surface

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

The OpenID Provider and the Management API are UI-free, yet several surfaces need a browser front end: the management
console, the interactive sign-in and consent bundle, the playground and the conformance harness with its headless
runner. The management surface also has to show the security boundary between the platform, a team and a tenant.

Built as one-off UIs, these surfaces would duplicate the design system, the management client, the boot sequence and the
build tooling, and they would drift apart. A single SPA that route-gates every surface would couple the management
console to the end-user sign-in page and widen the blast radius of one bundle. Every surface also has to run unchanged in
every tier (sandbox, test, production), so nothing tier-specific can be baked in at build time.

A boot container that discovers a surface's runtime configuration, stores it, themes the app and shows a loading or error
state depends on both the store and the UI package. If no shared package may hold it, every standalone app carries its
own copy, and the copies drift in their tests. The surface name that the host maps to client coordinates is an open
dictionary, so a closed union of surface names in a library would be the only thing a new surface had to edit a library
for.

## Decision

**One TypeScript pnpm workspace under `frontend/` in the product repository holds every web surface as a thin app shell
over shared, scoped `@ncode/*` packages, and a layering rule keeps each layer independently testable.**

- **Surfaces and toolchain.** The surfaces are `apps/auth`, `apps/mgmt`, `apps/playground`, `apps/conformance` and the
  headless `apps/conformance-runner`. The workspace is React, Material UI, Redux Toolkit with RTK Query, Vite, Vitest
  with React Testing Library, Storybook, Turborepo, ESLint and Prettier, with TypeScript `strict`. The product's
  repository holds it ([ADR-1501](1501-a-projects-top-level-folder-and-name-say-whether-it-ships-and-what-it-is.md),
  [ADR-1502](1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md)).
- **A gate of its own.** The .NET build never sees the workspace. The frontend has its own gate and CI job:
  `pnpm install --frozen-lockfile`, `format:check`, `lint`, `typecheck`, `build` and `test:report`. `node_modules/`,
  `dist/`, `.turbo/` and coverage output are git-ignored.
- **Shared packages.** Each is used only through its public entry point:
  - `@ncode/config` discovers the runtime configuration.
  - `@ncode/store` holds the store, the typed hooks, the session slice and the RTK Query `managementApi` with its base
    query and cache tags.
  - `@ncode/api` holds the management wire contracts and the platform, team and tenant scope model, free of UI and Redux.
  - `@ncode/ui` is the stateless design system and its theme.
  - `@ncode/shell` holds the shared stateful containers.
  - `@ncode/auth` holds the public-client protocol helpers.
  - `@ncode/conformance-core`, `@ncode/test-support` and `@ncode/vite-config` serve the conformance surfaces, the tests
    and the build.
- **Layering is the testability invariant.** Presentational components are stateless and render in Storybook and under
  React Testing Library without a store. Containers at the top of a screen wire Redux and RTK Query. Non-trivial logic
  lives in UI-free, unit-tested modules. RTK Query is the default for server state. Breaking the layering is a review
  failure.
- **Where a container lives.** `@ncode/ui` depends on no store, and `@ncode/store` and `@ncode/config` hold no UI. A
  container that more than one surface uses, such as `BootstrapGate`, lives in `@ncode/shell`, the one package that may
  depend on both. A container that one surface uses, such as the console's `SessionGate`, stays in its app.
- **One bundle, every tier.** A bundle is built once and nothing tier-specific is baked in. On boot it fetches
  `/config/bootstrap.json` from its serving host, which owns that document; an OpenID surface then reads the OP's
  discovery. The bootstrap issuer is the serving tenant's
  ([ADR-1182](1182-the-frontend-bootstraps-issuer-is-the-serving-tenants-and-its-api-base-is-root-relative.md)).
- **A surface-aware bootstrap.** A bundle sends `?surface=<name>`. `FrontendBootstrapOptions.Surfaces` maps the name to an
  override: its client id and scopes replace the default, its feature flags merge per key and its branding falls back. An
  unknown or absent surface gets the default.
- **An open surface name.** `SurfaceKind` is the shipped names plus `(string & {})`, an open vocabulary
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), so a new surface needs no change to `@ncode/config`.
  Each standalone OpenID surface declares itself once, in `main.tsx`, by passing `createRuntimeConfigLoader({ surface })`
  to `BootstrapGate`. The console passes `createBffRuntimeConfigLoader()`
  ([ADR-1438](1438-the-management-console-holds-no-token-and-reads-its-session-from-its-bff.md)). `loadRuntimeConfig`
  defaults the origin to `location.origin`, the serving host of every bundle because a standalone dev server proxies the
  bootstrap document. No app module binds the origin or the surface.
- **The gate takes its loader as a prop.** `BootstrapGate`'s `load` is a `RuntimeConfigLoader`
  (`(signal: AbortSignal) => Promise<RuntimeConfig>`), so a test passes its own loader instead of mocking a module. A new
  loader abandons an unfinished load, a failure belongs to the loader that produced it, only the current load reports, and
  configuration that reaches the store any other way wins. Its loading and error views are the stateless `BootstrapScreen`
  in `@ncode/ui`, with stories, rendered in the default theme because no branding is known yet.
- **Serving models.** Each app's `dev` script encodes how it is served. Backend-served `auth` and BFF-served `mgmt` run
  `vite build --watch`; standalone `playground` and `conformance` run Vite dev servers. Adding a surface to the AppHost
  is one `AddViteApp` or `AddJavaScriptApp` line
  ([ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md)).
- **Scopes are distinct contexts.** The management surface models platform, team and tenant scopes as distinct browsing
  contexts with their own navigation, cache tags and authorization gating, never one filtered list
  ([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)).
- **Branding through a provider.** Theme tokens sit behind a provider in `@ncode/ui`, and a tenant's branding arrives
  through the bootstrap document
  ([ADR-1253](1253-a-tenant-brands-its-sign-in-pages-through-settings-and-the-bundle-mounts-over-the-hosts-page.md)).

## Options considered

- **One SPA with route-gated surfaces.** Rejected: one origin and one bundle weaken the security boundary, enlarge the
  blast radius and couple the console to the end-user sign-in page.
- **One-off UIs per surface.** Rejected: they duplicate the design system, the client, the boot sequence and the tooling,
  and they drift.
- **Build-time tier configuration.** Rejected: per-environment bundles forfeit one bundle for every tier and invite a
  mismatch between bundle and configuration.
- **Nx.** Rejected: its generator and plugin surface is heavier than a package-and-apps graph needs; pnpm workspaces with
  Turborepo are lighter and sufficient.
- **Yarn Plug'n'Play or zero-installs.** Rejected: Plug'n'Play still needs patches for the Vite, Storybook and MUI stack,
  and zero-installs fills the repository with binary archives. pnpm's symlinked layout gives most of the strictness
  without that cost.
- **A closed `SurfaceKind`.** Rejected: a misspelled surface gets the host default, which fails visibly at the first
  sign-in, while a closed union makes every new surface edit a library.
- **The boot gate in `@ncode/ui` or `@ncode/store`.** Rejected: a store-backed container breaks the stateless, store-free
  `@ncode/ui`, and in `@ncode/store` it would make the UI-free state package depend on Material UI.
- **A gate that loads by itself from a `surface` prop.** Rejected: a test must mock a module to control the load, and a
  surface that loads differently cannot use it.
- **A `bootstrap.ts` per app.** Rejected: the copies differ by one string.
- **A `mountSurface` helper owning `createRoot`, the store `Provider` and the gate.** Rejected: `main.tsx` stays the
  explicit composition root, and hiding the `Provider` hides the requirement that the gate sits inside it.
- **One shared platform, as decided.** Redux Toolkit with RTK Query gives one state model for server and client state,
  matching the stateless and stateful layering.

## Consequences

- Every surface inherits the design system, the management client, the boot sequence and the build tooling, and a fix to
  the gate lands once. Its test suite covers abandoned and late loads, StrictMode remounts and a loader replaced after a
  failure.
- The Node and pnpm toolchain stays isolated under `frontend/` and does not touch the .NET Definition of Done.
- The layering is a maintained convention, written in [`frontend/README.md`](../../frontend/README.md). State in a
  presentational component or logic in a component breaks Storybook and unit isolation.
- The serving host and the bundle are coupled through the bootstrap document: the host must publish one the bundle
  understands. The management REST contracts are a typed dependency of `@ncode/api`, so a contract change reaches the
  client types.
- A surface's only boot-time binding is the `surface` it passes in `main.tsx`, and the next container that two surfaces
  share has a home with the dependencies it needs.

## References

- [`frontend/`](../../frontend/), [`frontend/README.md`](../../frontend/README.md),
  [`frontend/packages/shell`](../../frontend/packages/shell),
  [`packages/ui/src/components/BootstrapScreen.tsx`](../../frontend/packages/ui/src/components/BootstrapScreen.tsx),
  [`packages/config/src/loader.ts`](../../frontend/packages/config/src/loader.ts)
- Issues [#125](https://github.com/NCodeGroup/identity/issues/125), [#40](https://github.com/NCodeGroup/identity/issues/40)
  and [#150](https://github.com/NCodeGroup/identity/issues/150)
- [ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md)
  (the sign-in bundle's stable-locator contract),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) (open vocabularies),
  [ADR-1157](1157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md) (the shared build preset)
