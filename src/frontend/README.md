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
pnpm dev                  # run the WHOLE frontend stack (Turborepo): dev servers + build-watch
pnpm --filter @ncode/app-mgmt dev    # run just the Management console
pnpm test                 # run all unit tests
pnpm lint                 # lint all packages
pnpm typecheck            # type-check all packages
pnpm --filter @ncode/ui storybook    # component workbench
pnpm build                # build all packages/apps
```

### The `dev` script encodes each app's serving model

`pnpm dev` is `turbo run dev`, which runs **every** app's own `dev` script — so a new app joins the
full-stack launch automatically just by defining one. What that script does depends on how the app is
served:

- **Backend-served SPAs** — served by the .NET host from a static `dist` (e.g. `apps/auth`, which the
  Playground serves through the interactive-login endpoint) — use **`vite build --watch`**. This keeps
  `dist` continuously rebuilt so a browser refresh shows edits. (There is no HMR dev server because the
  .NET host, not Vite, serves these bundles.)
- **Standalone SPAs** — served directly by Vite on their own port (e.g. `apps/mgmt`) — use **`vite`**,
  a full HMR dev server.

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

Backend-served bundles (e.g. `apps/auth`, served by the OpenID Provider host) get this document from the
host at runtime: `NCode.Identity.Server` binds the `Frontend` configuration section and registers the
endpoint through the single `IIdentityServerBuilder.AddFrontendBootstrap(configuration)` seam — the host's
existing `MapEndpointGroups()` call then publishes it (API base, issuer, client id, scopes, feature flags,
branding), falling back to the serving request's own origin for the API base and issuer when they are unset
— so a single-origin host needs no configuration.

## Authentication (authorization-code + PKCE)

The Management console authenticates with OAuth 2.0 **authorization-code + PKCE** to obtain a
`urn:ncode:management`-audience access token (authority is role-based `GlobalAdmin`; any management
scope binds the audience — see [ADR-0044](../../docs/adr/0044-management-api-authentication-and-globaladmin-bootstrap.md)).

- `@ncode/auth` is a framework-free, fully unit-tested package: PKCE (`generateCodeVerifier`,
  `deriveCodeChallenge`), the authorize redirect, `state` CSRF validation, `exchangeCodeForToken` /
  `refreshAccessToken`, and `getSubject` (JWT claims). Randomness, SHA-256, `fetch`, and storage are
  all injected.
- The app's `AuthGate` completes the redirect callback into the `auth` slice, or shows the sign-in
  screen to start the flow. **When no `clientId` is discovered it runs open** (local dev), so the
  console renders without a login.
- The SPA's public `clientId` and `scopes` come from the runtime bootstrap document — nothing is baked
  in. End-to-end sign-in additionally needs a registered public SPA client and the interactive login UI
  (backlog `T2`).

## Running the full stack (VS Code F5)

Pick **`Aspire: Full stack (F5)`** in the Run and Debug view (or press F5) — a **.NET Aspire** AppHost
([ADR-0071](../../docs/adr/0071-aspire-local-dev-orchestrator.md)) that orchestrates the whole local stack
as one resource graph: the Playground OP host (`api`), the interactive-login build-watcher (`auth`), and the
Management console Vite dev server (`mgmt`). Aspire gates each UI resource on the API being **healthy**
(`WaitFor` + `/health`), so the browser UIs no longer race the API, and its **dashboard** is one place for
ordered startup, logs, and traces.

One-time setup (**required** before the first launch, and again whenever dependencies change): run
[`build/frontend-install.ps1`](../../build/frontend-install.ps1) to install the pnpm workspace and enable the
Corepack `pnpm` shim. The AppHost does **not** auto-install (concurrent per-app installs collide on a pnpm
workspace), so this script is the single installer. The AppHost automatically puts the repo-pinned
**NVS-managed Node 22** on `PATH` for the frontend resources, so you only need `nvs add 22` once — no global
`nvs link` required.

Each app keeps its own `dev` script per its serving model (see "The `dev` script encodes each app's serving
model" above): the interactive login surface (`apps/auth`) build-watches into its `dist`, which the Playground
serves under `/account/*` — on a cold start give its first build a moment, then refresh `/account/login` if you
beat it there; `apps/mgmt` is a standalone HMR dev server pinned to `http://localhost:5173`. Adding a surface is
one `AddViteApp`/`AddJavaScriptApp` line in the AppHost.

> Dev connectivity: the Playground opts into `AddDeveloperCors` so the browser UI (`http://localhost:5173`) can read
> discovery and call the management API cross-origin. The UI still renders in **open mode** and management calls return
> `401` until a public SPA client is seeded and the interactive login (`T2`) exists — tracked under backlog `U1`/`U7`.

## Gotchas (hard-won)

- **Internal packages are consumed as TypeScript source, not built `.d.ts`.** Each `@ncode/*` package
  exposes `src/index.ts` via its `main`/`types`; Vite and Vitest bundle the source directly. There is
  **no `tsc --build`/`composite`/project-references** — those emit `.d.ts` that get resolved _alongside_
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
- **jsdom + undici cross-realm `AbortSignal`.** Under `environment: 'jsdom'`, `fetch`/`Request`/`Response`
  stay Node's (undici) globals while jsdom overrides `AbortController`/`AbortSignal`. `fetchBaseQuery`
  builds `new Request(url, { signal })` from the global (jsdom) `AbortController`, and undici's `Request`
  rejects the foreign-realm signal — _"RequestInit: Expected signal to be an instance of AbortSignal"_ —
  which surfaces as a spurious query failure (e.g. _"Failed to load tenants"_) **before** any mocked
  `fetch` runs. Node's original `AbortController` is unreachable once jsdom shadows the global, and
  `undici` is not a resolvable dependency, so [`apps/mgmt/vitest.setup.ts`](apps/mgmt/vitest.setup.ts)
  wraps `Request` to drop the incompatible jsdom signal during construction. It is a test-only shim
  (the browser is a single consistent realm); any app whose tests exercise RTK Query needs the same
  setup shim.
- **Single-use side effects must be guarded by a run-once ref, not an effect-cancellation flag.**
  React StrictMode (on in dev via [`apps/mgmt/src/main.tsx`](apps/mgmt/src/main.tsx)) intentionally
  double-invokes every effect (setup → cleanup → setup) on the same component instance. An effect that
  consumes a **one-shot** resource — e.g. [`AuthGate`](apps/mgmt/src/AuthGate.tsx) completing the
  authorization-code callback, which spends a single-use `code` and removes the one-shot PKCE
  transaction from `sessionStorage` — must run **exactly once**. A `let cancelled = false` + cleanup
  flag does **not** achieve this: the second invocation re-enters the effect body and re-reads the
  already-consumed resource, dead-ending (here, on _"No in-flight authorization transaction was
  found."_). Guard with a `useRef(false)` set the moment the work starts; refs persist across the
  StrictMode remount, so the second invocation short-circuits. Set the ref only when the work actually
  begins (not on an early bail-out such as pre-discovery `authConfig === null`) so the single attempt
  is not wasted.
- **Management writes are JSON Patch + `If-Match`, not whole-object PUT.** The Management update endpoints
  bind `JsonPatchDocument<T>` and hydrate the current resource before applying the document, and they gate
  on an `If-Match` concurrency token (optimistic concurrency). RTK Query mutations express an edit as a
  `replace`-only patch built from the stable wire property names via the UI-free
  [`replacePatch`](packages/api/src/jsonPatch.ts) helper (`/displayName`, `/domainName`, …), and send the
  resource's `concurrencyToken` as the `If-Match` header. Creates are plain `POST` (the id is
  server-generated; the response carries the new `concurrencyToken`), deletes are `DELETE` (a `409` means
  the resource still has dependents). Invalidate both the `LIST` tag and the per-id tag so the scoped
  caches reconcile. Server failures are RFC 7807 Problem Details; surface them through
  [`apiErrorMessage`](packages/store/src/apiError.ts) rather than re-deriving messages in each container.
- **A `data-testid` on a MUI control lands on the root, not the native input.** `<Switch data-testid=…>`
  (and friends) forward the attribute to the outer element, so `getByTestId(...)` returns a `<span>` and
  matchers like `.toBeChecked()` fail. `TextField`'s `slotProps.htmlInput` _does_ reach the input (its
  type has an index signature), but `Switch`'s `slotProps.input` / `inputProps` types reject `data-*`.
  Prefer querying the control by role and accessible name instead —
  `getByRole('checkbox', { name: 'Disabled' })` (the `FormControlLabel` supplies the name) — which is both
  type-safe and closer to how a user finds it.
