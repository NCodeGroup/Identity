# NCode.Identity frontend platform

A single, cohesive TypeScript presentation platform shared by every NCode.Identity UI surface —
Management, Auth/Consent/Login, Playground, and OIDC conformance. Decision record:
[ADR-0064](../../docs/adr/0064-frontend-presentation-platform.md).

> **Monorepo now, clean split later.** This lives in the monorepo under `src/frontend/` today. At GA
> the repo may be reorganized, so every shared concern is a scoped `@ncode/*` package consumed only
> through its public entry point (no deep cross-package imports). Extraction should be a folder move,
> not a refactor.

## Stack

- **React 18 + Material UI (MUI)** for the view layer.
- **Redux Toolkit + RTK Query** for client and server state.
- **Vite** build, **Vitest + React Testing Library** unit tests, **Storybook** for component
  verification, **TypeScript (strict)** everywhere.
- **pnpm workspaces + Turborepo** orchestrate the package/app graph.

This toolchain is intentionally **outside** the .NET `dod.ps1` inner loop; it has its own scripts.

### Package manager — pnpm via Corepack

The pnpm version is pinned in the root [`package.json`](package.json) `packageManager` field and
activated with **Corepack** (built into Node ≥ 20), so every machine and CI runner uses the exact same
pnpm with no global install:

```sh
corepack enable            # one-time: let Corepack shim pnpm/yarn/npm
corepack prepare --activate # activate the pinned pnpm@… from packageManager
```

We deliberately use pnpm's strict, symlinked `node_modules` rather than Yarn Plug'n'Play or
zero-installs: PnP needs patches for the Vite + Storybook + emotion/MUI stack and zero-installs would
bloat the repo (see [ADR-0064](../../docs/adr/0064-frontend-presentation-platform.md)).

The Node major version is pinned in [`.node-version`](.node-version) (Node 22), honored by NVS, nvm,
and fnm, so each repo selects its own Node. With NVS: `nvs auto on` switches automatically on `cd`.

## Layout

```
src/frontend/
  packages/
    config/   @ncode/config  — tier-agnostic runtime discovery (no UI)
    api/      @ncode/api     — RTK Query client + management scope model (no UI)
    store/    @ncode/store   — Redux store assembly, slices, typed hooks
    ui/       @ncode/ui      — MUI design system + stateless components + Storybook
  apps/
    mgmt/     @ncode/app-mgmt — Management console (first surface)
```

## Architectural invariants

1. **Stateless presentational components.** Components in `@ncode/ui` (and app `components/`) are
   prop-driven and hold no store/fetch state, so they render in Storybook and under RTL in isolation.
   Local state is allowed only when intrinsic to a control and it does not break isolated rendering.
2. **Stateful containers at the top.** State and side effects live in container components
   (`apps/*/src/containers/`) and the shell, which wire Redux + RTK Query and pass plain props down.
3. **UI-free services/logic are unit-tested directly.** Validation, discovery parsing, PKCE, and the
   global-vs-tenant boundary rules live in plain modules (`@ncode/config`, `@ncode/api/scope`) with
   their own Vitest suites.
4. **RTK Query is the default for server state.** Hand-rolled thunks are the exception.
5. **One bundle, every tier.** No tier value is baked at build time; `@ncode/config` discovers the API
   base, issuer, auth endpoints, feature flags, and branding at boot from the serving host's
   `/config/bootstrap.json` + the OP's `/.well-known/openid-configuration`.
6. **The global vs tenant security boundary is first-class** — distinct browsing contexts, cache tags,
   and authorization gating, never one list with a filter.
7. **Branding is a runtime seam.** `@ncode/ui` themes from runtime `BrandingConfig`; per-tenant
   branding is additive later through the same discovery channel.
8. **`type` over `interface`, always.** All declarations — DTOs, props, unions — use `type` aliases,
   standardizing on the single most capable mechanism (unions, intersections, mapped/conditional
   types) rather than mixing the two.

## Common commands

Run from `src/frontend/`:

```sh
pnpm install              # install the workspace
pnpm dev                  # run app dev servers (Turborepo)
pnpm --filter @ncode/app-mgmt dev    # run just the Management console
pnpm test                 # run all unit tests
pnpm lint                 # lint all packages
pnpm typecheck            # type-check all packages
pnpm --filter @ncode/ui storybook    # component workbench
pnpm build                # build all packages/apps
```

## Runtime configuration

The serving host must publish `/config/bootstrap.json`:

```json
{
  "apiBaseUrl": "https://id.example",
  "issuer": "https://id.example",
  "features": { "dcr": true },
  "branding": { "displayName": "Acme", "primaryColor": "#6750A4", "mode": "light" }
}
```

For local development the Management app ships a dev copy at
[`apps/mgmt/public/config/bootstrap.json`](apps/mgmt/public/config/bootstrap.json) pointing at the
Playground host.

## Gotchas (hard-won)

- **Internal packages are consumed as TypeScript source, not built `.d.ts`.** Each `@ncode/*` package
  exposes `src/index.ts` via its `main`/`types`; Vite and Vitest bundle the source directly. There is
  **no `tsc --build`/`composite`/project-references** — those emit `.d.ts` that get resolved *alongside*
  the source, producing "two different types ... unrelated" errors. Package `build` is type-check only
  (`tsc --noEmit`); only the app emits a bundle.
- **RTK Query store assembly.** `@reduxjs/toolkit/query/react` (used by `createApi`) bundles its own
  copies of the core `Reducer`/`Middleware`/`Tuple` declarations, which TypeScript treats as nominally
  distinct from the root `@reduxjs/toolkit` entry used by `configureStore`. The store in
  [`packages/store/src/store.ts`](packages/store/src/store.ts) reconciles this with three deliberate
  moves — keep them together:
  1. build a single root reducer with `combineReducers` and cast the API reducer: `managementApi.reducer as Reducer`;
  2. cast the API middleware: `getDefaultMiddleware().concat(managementApi.middleware as Middleware)`;
  3. include `preloadedState` **conditionally** (`...(preloadedState !== undefined ? { preloadedState } : {})`) — passing it unconditionally defeats `configureStore`'s middleware-tuple inference.
- **Testing `fetchBaseQuery`.** RTK calls `fetch` with a `Request` object, not a URL string. Mocks must
  read `input instanceof Request ? input.url : String(input)` and return a real `Response`
  (`new Response(JSON.stringify(body), { status, headers })`) — a plain object lacks `.clone()`, which
  `fetchBaseQuery` calls.

