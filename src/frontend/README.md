# NCode.Identity frontend platform

A single, cohesive TypeScript presentation platform shared by every NCode.Identity UI surface —
Management, Auth/Consent/Login, Playground, and OIDC conformance. Decision record:
[ADR-0064](../../docs/decisions/0064-frontend-presentation-platform.md).

> **Monorepo now, clean split later.** This lives in the monorepo under `src/frontend/` today. At GA
> the repo may be reorganized, so every shared concern is a scoped `@ncode/*` package consumed only
> through its public entry point (no deep cross-package imports). Extraction should be a folder move,
> not a refactor.

## Stack

- **React 19 + Material UI (MUI) 9** for the view layer.
- **Redux Toolkit + RTK Query** for client and server state.
- **Vite** build, **Vitest + React Testing Library** unit tests, **Storybook** for component
  verification, **TypeScript (strict)** everywhere.
- **pnpm workspaces + Turborepo** orchestrate the package/app graph.

This toolchain is intentionally **outside** the .NET `dod.ps1` inner loop; it has its own scripts.

### Package manager — pnpm, pinned by `packageManager`

The pnpm version is pinned in the root [`package.json`](package.json) `packageManager` field, so every machine and CI
runner uses the exact same pnpm. Node no longer bundles Corepack, so nothing activates it implicitly:
[`build/frontend-install.ps1`](../../build/frontend-install.ps1) installs the pinned version with the pinned Node's
npm, and CI installs it with `pnpm/action-setup`. A globally installed pnpm 10 or later also switches to the pinned
version by itself. The pin stays on the newest pnpm major Dependabot supports (10), and Dependabot does not bump it, so
it moves only by a deliberate change (issue [#153](https://github.com/NCodeGroup/Identity/issues/153),
[ADR-0152](../../docs/decisions/0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md)).

Every pnpm setting lives in [`pnpm-workspace.yaml`](pnpm-workspace.yaml), not `.npmrc` or the `package.json` `pnpm`
field. Dependency install scripts are denied unless reviewed: `strictDepBuilds` fails an install on any script nobody
has decided about, and `allowBuilds` records each decision. A version must also be published for three days before pnpm
resolves it (`minimumReleaseAge`), the same window Dependabot waits for its npm updates
([ADR-0153](../../docs/decisions/0153-one-release-age-window-gates-frontend-dependencies.md)).

We deliberately use pnpm's strict, symlinked `node_modules` rather than Yarn Plug'n'Play or
zero-installs: PnP needs patches for the Vite + Storybook + emotion/MUI stack and zero-installs would
bloat the repo (see [ADR-0064](../../docs/decisions/0064-frontend-presentation-platform.md)).

The Node major version is pinned once, in [`.node-version`](.node-version) (Node 26). CI reads it
(`node-version-file`), the Aspire AppHost and `build/frontend-install.ps1` read it to select the NVS install, and NVS
and fnm honor it, so each repo selects its own Node (nvm reads only `.nvmrc`). `engines.node` states the same
major as the floor — it is also what makes Turborepo's cache miss when the runtime changes — and `@types/node` tracks
that major, not the newest release
([ADR-0147](../../docs/decisions/0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md)). Moving to a
new major changes `.node-version`, `engines.node`, the `@types/node` ranges, and the Node image of the host
[`Dockerfile`](../NCode.Identity.OpenId.Host/Dockerfile) together; the `Host image` workflow checks that image's
major. With NVS: `nvs auto on` switches automatically on `cd`; it only lasts for the current shell, so persist it in
your shell profile:

```powershell
# pwsh — $PROFILE.CurrentUserAllHosts (profile.ps1; covers the terminal and the VS Code PowerShell host)
if (Get-Command nvs -ErrorAction SilentlyContinue) { nvs auto on }
```

```sh
# Git Bash — ~/.bashrc (and have ~/.bash_profile source ~/.bashrc, since Git Bash starts a login shell)
export NVS_HOME="$HOME/AppData/Local/nvs"
[ -s "$NVS_HOME/nvs.sh" ] && . "$NVS_HOME/nvs.sh"
command -v nvs >/dev/null 2>&1 && nvs auto on
```

## Layout

```
src/frontend/
  packages/
    config/   @ncode/config  — tier-agnostic runtime discovery (no UI)
    api/      @ncode/api     — RTK Query client + management scope model (no UI)
    store/    @ncode/store   — Redux store assembly, slices, typed hooks
    ui/       @ncode/ui      — MUI design system + stateless components + Storybook
    shell/    @ncode/shell   — stateful app-shell containers shared by the surfaces (the runtime-config BootstrapGate)
    conformance-core/ @ncode/conformance-core — framework-free conformance logic (discovery, audit, spec-coverage, probes)
    test-support/ @ncode/test-support — test-only Vitest support shared by every workspace (the console guard and the Testing Library wait timeout)
    vite-config/  @ncode/vite-config — the Vite/Vitest preset every workspace builds and tests from
  apps/
    mgmt/     @ncode/app-mgmt — Management console (first surface)
    auth/     @ncode/app-auth — Interactive login/consent surface (OP-hosted under /account/*)
    playground/   @ncode/app-playground — OAuth 2.1 / OIDC authorization-code + PKCE flow explorer (dev server :5174)
    conformance/  @ncode/app-conformance — Security-posture & conformance-readiness dashboard (dev server :5175)
    conformance-runner/ @ncode/app-conformance-runner — headless CLI probing the confidential/server-initiated legs (ADR-0084 Phase 3)
```

## Architectural invariants

1. **Stateless presentational components.** Components in `@ncode/ui` (and app `components/`) are
   prop-driven and hold no store/fetch state, so they render in Storybook and under RTL in isolation.
   Local state is allowed only when intrinsic to a control and it does not break isolated rendering.
   They render as a pure function of their props: ambient values such as the clock come in as props
   (e.g. `GrantTable`'s `now`), never from a read during render — the React lint rules (`purity`) flag it.
2. **Stateful containers at the top.** State and side effects live in container components
   (`apps/*/src/containers/`) and the shell, which wire Redux + RTK Query and pass plain props down. A container that
   more than one surface needs (the runtime-config `BootstrapGate`) lives in `@ncode/shell`, the one package that may
   depend on both `@ncode/store` and `@ncode/ui`: `@ncode/ui` stays store-free and `@ncode/store` stays UI-free
   ([ADR-0156](../../docs/decisions/0156-shared-app-shell-containers-live-in-ncode-shell.md)).
3. **UI-free services/logic are unit-tested directly.** Validation, discovery parsing, PKCE, and the
   global-vs-tenant boundary rules live in plain modules (`@ncode/config`, `@ncode/api/scope`) with
   their own Vitest suites.
4. **RTK Query is the default for server state.** Hand-rolled thunks are the exception.
5. **One bundle, every tier.** No tier value is baked at build time; `@ncode/config` discovers the API
   base, issuer, auth endpoints, feature flags, and branding at boot from the serving host's
   `/config/bootstrap.json` + the OP's `/.well-known/openid-configuration`.
6. **The global vs tenant security boundary is first-class** — distinct browsing contexts, cache tags,
   and authorization gating, never one list with a filter.
7. **Branding is a runtime seam.** `@ncode/ui` themes from runtime `BrandingConfig`. The interactive
   surface (`apps/auth`) reads its tenant's branding from the JSON block the host embeds in every page it
   serves with the bundle (`<script type="application/json" id="branding">`, `BRANDING_ELEMENT_ID`), parses
   it with `parseBrandingConfig`, and mounts at once over the server-rendered page (ADR-0194, ADR-0253).
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
pnpm run test:report      # same, plus per-package JUnit (test-results/) and coverage (coverage/) — what CI runs;
                          # every package runs to completion even when one fails, so the report is never truncated;
                          # fails when a package drops below its coverage floor
pnpm lint                 # lint all packages (warnings fail: --max-warnings 0)
pnpm typecheck            # type-check all packages, including tests, stories, and setup files
pnpm --filter @ncode/ui storybook    # component workbench
pnpm build                # build all packages/apps
```

### Test gates — warnings are errors

A green run means more than "no assertion failed" ([ADR-0151](../../docs/decisions/0151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md)).
The gates are set once, in the [`@ncode/vite-config`](packages/vite-config) preset that every workspace's Vite or Vitest
config calls with its own coverage floors ([ADR-0157](../../docs/decisions/0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md)):

- **A test that logs `console.error` or `console.warn` fails.** Every workspace's `vitest.setup.ts` registers
  `failOnConsole` from [`@ncode/test-support`](packages/test-support). The usual cause is a React
  _"not wrapped in act(...)"_ warning: the test asserted before an async update (a resolved promise, an RTK Query
  fetch, a transition) settled. Await the settled UI with `findBy…` / `waitFor` rather than wrapping calls in `act`.
  When logging _is_ the behavior under test, mock the method in that test
  (`vi.spyOn(console, 'error').mockImplementation(() => {})`) and assert on the spy. The one built-in exemption is
  Redux Toolkit's load-dependent "…StateInvariantMiddleware took Nms" timing warning, which the guard drops: it would
  otherwise fail store-backed tests at random under coverage. Keep that list to timing diagnostics.
- **Lint warnings fail** (`--max-warnings 0`), and so does a stale `eslint-disable` comment.
- **A test that makes no assertion fails** (`expect.requireAssertions`).
- **Tests, stories, and setup files are type-checked** — no tsconfig excludes them.
- **Coverage measures every source file and holds a floor.** Every workspace collects `src/**/*.test.{ts,tsx}` and
  measures all `.ts`/`.tsx` sources except tests, stories, its entry point (`main.ts[x]`), and its barrel
  (`index.ts`), against the `coverage` floors its config passes to the preset: the level reached, rounded down.
  Vitest enforces them whenever coverage is collected (`pnpm run test:report`, which CI runs). When a change raises a
  workspace's coverage, raise its floor in the same change; lowering one needs a stated reason.

A new workspace joins these gates by adding `"@ncode/test-support": "workspace:*"` and
`"@ncode/vite-config": "workspace:*"` to its `devDependencies`, a `vitest.setup.ts` that calls
`failOnConsole({ beforeEach, afterEach })` (listed in the tsconfig `include`), `--max-warnings 0` on its `lint` script,
and a one-call config: `defineTestConfig({ environment, coverage })` in `vitest.config.ts` for a package, or a surface
preset in `vite.config.ts` for an app (below). `@ncode/test-support` itself keeps a hand-written config that states the
same gates, because the preset depends on it and turbo rejects a cyclic workspace graph.

### The `dev` script encodes each app's serving model

`pnpm dev` is `turbo run dev`, which runs **every** app's own `dev` script — so a new app joins the
full-stack launch automatically just by defining one. What that script does depends on how the app is
served:

- **Backend-served SPAs** — served by the .NET host from a static `dist` (e.g. `apps/auth`, which the
  Playground serves through the interactive-login endpoint) — use **`vite build --watch`**. This keeps
  `dist` continuously rebuilt so a browser refresh shows edits. (There is no HMR dev server because the
  .NET host, not Vite, serves these bundles.) Their config is `defineBackendServedSurfaceConfig({ coverage })`, which
  builds with a relative base: the host serves the bundle beneath each tenant's route and sets the document's
  `<base href>` there. The host serves only the bundle's `assets` directory, its content-hashed files, and marks them
  immutable, so the preset pins `build.assetsDir` to `assets` (ADR-0254).
- **Standalone SPAs** — served directly by Vite on their own port (e.g. `apps/mgmt`) — use **`vite`**,
  a full HMR dev server. Their config is `defineStandaloneSurfaceConfig({ port, bootstrapHost, coverage })`, which pins
  the port with `strictPort` (the surface's redirect URI, CORS origin, and AppHost endpoint all assume it) and proxies
  the bootstrap document to the OP host (see [Runtime configuration](#runtime-configuration)).

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

Every bundle gets this document from the host at runtime — there is no static dev copy to drift.

Each standalone surface boots through the one [`BootstrapGate`](packages/shell/src/BootstrapGate.tsx) from
`@ncode/shell`, given the surface's loader in its `main.tsx`:

```tsx
const loadRuntimeConfig = createRuntimeConfigLoader({ surface: 'playground' });
// …
<BootstrapGate load={loadRuntimeConfig}>
  <App />
</BootstrapGate>;
```

The gate shows the stateless `BootstrapScreen` (from `@ncode/ui`, default theme) until the configuration is in the
store, then renders the app themed by the discovered branding. The loader defaults the serving origin to the page's own
`location.origin`, so the surface name is the only per-app binding. Pass the gate a stable loader: a new one abandons an
unfinished load and starts over.

> **Surface-aware.** Each bundle declares its `SurfaceKind` when it loads config, which `@ncode/config` sends as
> `?surface=` (for example `/config/bootstrap.json?surface=playground`). The host maps that to a per-surface override
> (`FrontendBootstrapOptions.Surfaces`) so every surface gets its **own** public client id and scopes from the one
> endpoint — the Playground discovers `playground-spa`, while the read-only conformance dashboard gets no client at all
> ([ADR-0082](../../docs/decisions/0082-playground-and-conformance-surfaces.md)). A surface with no override, or no
> `?surface=`, receives the host default (the Management console's). `SurfaceKind` is an open vocabulary: it lists the
> shipped surfaces but accepts any name, so a new surface needs only its host override, not a change to
> `@ncode/config`.

`NCode.Identity.Server` binds the `Frontend` configuration section and registers the endpoint through the
single `IIdentityServerBuilder.AddFrontendBootstrap(configuration)` seam — the host's existing
`MapEndpointGroups()` call then publishes it (API base, issuer, client id, scopes, feature flags, branding). An unset
issuer is the issuer of the tenant that serves the surface — never the request's origin — and an unset API base is the
serving request's own origin, so a single-origin host needs no configuration
([ADR-0182](../../docs/decisions/0182-the-frontend-bootstrap-issuer-is-the-serving-tenants-not-the-requests.md)).

- **Backend-served bundles** (e.g. `apps/auth`, served by the OpenID Provider host) fetch the document
  straight from their serving host.
- **Standalone bundles** (e.g. `apps/mgmt`, served by its own Vite dev server) don't share the OP's origin, so the
  Vite dev server **proxies** `/config/bootstrap.json` to the OP host instead of shipping a static copy (the
  `bootstrapHost` of `defineStandaloneSurfaceConfig` in [`@ncode/vite-config`](packages/vite-config/src/preset.ts)).
  The proxy target comes from the Aspire service-discovery variable the surface's resource receives via
  `WithReference(api)` ([ADR-0071](../../docs/decisions/0071-aspire-local-dev-orchestrator.md)), falling back to the pinned
  origin (`https://localhost:5001`) when run outside Aspire — so local dev exercises the real runtime endpoint.

## Authentication (authorization-code + PKCE)

The Management console authenticates with OAuth 2.0 **authorization-code + PKCE** to obtain a
`urn:ncode:management`-audience access token (authority is role-based `GlobalAdmin`; any management
scope binds the audience — see [ADR-0044](../../docs/decisions/0044-management-api-authentication-and-globaladmin-bootstrap.md)).

- `@ncode/auth` is a framework-free, fully unit-tested package: PKCE (`generateCodeVerifier`,
  `deriveCodeChallenge`), the authorize redirect, `state` CSRF validation, `exchangeCodeForToken` /
  `refreshAccessToken`, and `getSubject` (JWT claims). Randomness, SHA-256, `fetch`, and storage are
  all injected.
- The app's `AuthGate` completes the redirect callback into the `auth` slice, or shows the sign-in
  screen to start the flow. **When no `clientId` is discovered it runs open** (local dev), so the
  console renders without a login.
- The SPA's public `clientId` and `scopes` come from the runtime bootstrap document — nothing is baked
  in. End-to-end sign-in additionally needs a registered public SPA client and the OP-hosted interactive login
  surface (`apps/auth`). In development the host declares each surface's client under
  `DeveloperInteractiveSeed:SpaClients` (seeded by `AddDeveloperInteractiveSeed`), beside the `Frontend` section that
  publishes the same client id; the reference host does both in its `appsettings.Development.json`, where
  `playground-spa` requires consent so the flow explorer exercises the consent surface. The seed has no built-in
  clients ([ADR-0146](../../docs/decisions/0146-a-development-seeder-registers-only-the-clients-its-configuration-declares.md)),
  so a surface you add needs both entries.

## Running the full stack (VS Code F5)

Pick **`Aspire: Full stack (F5)`** in the Run and Debug view (or press F5) — a **.NET Aspire** AppHost
([ADR-0071](../../docs/decisions/0071-aspire-local-dev-orchestrator.md)) that orchestrates the whole local stack
as one resource graph: the Playground OP host (`api`), the interactive-login build-watcher (`auth`), the
Management console Vite dev server (`mgmt`), the flow-explorer Vite dev server (`playground`, :5174), and the
conformance-dashboard Vite dev server (`conformance`, :5175). Aspire gates each UI resource on the API being
**healthy** (`WaitFor` + `/health/ready`), so the browser UIs no longer race the API, and its **dashboard** is one place
for ordered startup, logs, and traces.

One-time setup (**required** before the first launch, and again whenever dependencies or the pinned Node/pnpm change):
run [`build/frontend-install.ps1`](../../build/frontend-install.ps1). It adds the Node major pinned in
[`.node-version`](.node-version) through NVS when it is missing, installs the pinned pnpm beside it, and installs the
pnpm workspace. The AppHost does **not** auto-install (concurrent per-app installs collide on a pnpm workspace), so
this script is the single installer. The AppHost automatically puts that **NVS-managed Node** (and the pnpm beside it)
first on `PATH` for the frontend resources — no global `nvs link` required. Before it starts any resource, the AppHost
also checks the toolchain and **stops at startup** (naming `build/frontend-install.ps1`) when no Node of the pinned
major is available, or when `node_modules` was installed by another pnpm than the `packageManager` pin or before the
last `pnpm-lock.yaml` change
([ADR-0287](../../docs/decisions/0287-the-apphost-fails-fast-when-the-frontend-toolchain-is-stale.md)).

> **Self-healing / repair.** If a surface fails at dev time with Vite `Failed to resolve import "react-redux"` (or any
> workspace dependency) — the symptom of `node_modules` symlinks lost out-of-band to a branch switch, antivirus, or an
> aborted install — just **re-run `build/frontend-install.ps1`**. After its fast `--frozen-lockfile` install it probes
> every workspace package's links and force-reinstalls only when one is actually missing/broken, so re-running it is
> the single, serial repair (never run a per-app `pnpm install` against this workspace — concurrent installs collide
> with `ERR_PNPM_EBUSY`).

Each app keeps its own `dev` script per its serving model (see "The `dev` script encodes each app's serving
model" above): the interactive login surface (`apps/auth`) build-watches into its `dist`, which the Playground
serves under `/account/*` — on a cold start give its first build a moment, then refresh `/account/login` if you
beat it there; `apps/mgmt` is a standalone HMR dev server pinned to `https://localhost:5173`, served with the ASP.NET
Core development certificate (`dotnet dev-certs https --trust`). Run directly with `pnpm dev`, every standalone
surface (`mgmt`, `playground`, `conformance`) serves HTTPS from the certificate `./build/frontend-install.ps1` exports
as PEM to `~/.ncode/identity/dev-cert` (override the directory with `NCODE_DEV_CERT_DIR`), and the dev server stops
with an error naming that script when the files are missing; under Aspire the AppHost's certificate
(`TLS_CONFIG_PFX`) is used instead. Adding a surface is one `AddViteApp`/`AddJavaScriptApp` line in the AppHost.

> Dev connectivity: the Playground opts into `AddDeveloperCors` so the browser UI (`https://localhost:5173`) can read
> discovery and call the management API cross-origin. Without a seeded public SPA client the UI renders in **open
> mode** and management calls return `401`; the development seeder (`AddDeveloperInteractiveSeed`) and the interactive
> login surface ([#125](https://github.com/NCodeGroup/Identity/issues/125), done) close that gap for local end-to-end sign-in (issues [#133](https://github.com/NCodeGroup/Identity/issues/133)/[#139](https://github.com/NCodeGroup/Identity/issues/139)).

## Gotchas (hard-won)

- **Internal packages are consumed as TypeScript source, not built `.d.ts`.** Each `@ncode/*` package
  exposes `src/index.ts` via its `main`/`types`; Vite and Vitest bundle the source directly. There is
  **no `tsc --build`/`composite`/project-references** — those emit `.d.ts` that get resolved _alongside_
  the source, producing "two different types ... unrelated" errors. Package `build` is type-check only
  (`tsc --noEmit`); only the app emits a bundle.
- **`@ncode/vite-config` is loaded by Node itself, so it follows stricter rules.** Vite bundles a `vite.config.ts` but
  leaves its bare imports external, so Node imports the preset's TypeScript source directly by stripping its types.
  It is one module, [`src/preset.ts`](packages/vite-config/src/preset.ts) (its `exports` point there, not at an
  `index.ts`), that uses only erasable syntax (`erasableSyntaxOnly`) and has no relative or workspace imports: Node
  cannot map the `.js` specifiers the other packages use to their `.ts` files. The configs that call it are not
  type-checked, so it validates its own options. Keep new presets in that module
  ([ADR-0157](../../docs/decisions/0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md)).
- **Turborepo re-runs a task when anything it reads changes.** A task's hash covers only its own package by default, so
  `test` and `typecheck` depend on a no-op `transit` task that links the package graph: editing an internal package
  re-runs its dependents' tests and type-checks (still in parallel), and `globalDependencies` re-runs everything when
  `tsconfig.base.json` or `eslint.config.js` changes. A new root file that tasks read belongs in `globalDependencies`.
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
- **Testing Library and test timeouts.** Every jsdom workspace's `vitest.setup.ts` calls
  `configureAsyncUtilTimeout(configure)`, which gives each `findBy…` and `waitFor` `ASYNC_UTIL_TIMEOUT_MS` (10 s) from
  `@ncode/test-support` in place of Testing Library's 1 s default. The preset gives every browser-simulating suite
  `BROWSER_TEST_TIMEOUT_MS` (30 s), at least three waits, instead of Vitest's 5 s default, so a stuck wait fails with
  Testing Library's DOM report rather than a bare test timeout. A jsdom test that renders a full MUI screen spends
  seconds in the first render (theme and emotion setup), and v8 coverage instrumentation — which CI always collects
  (`pnpm run test:report`) — slows it further on a shared runner: CI measured 2.7–7.1 s for single tests across the
  jsdom suites, so the defaults failed them intermittently. Both are hang guards, not performance assertions; the
  `environment: 'node'` suites (milliseconds per test) keep Vitest's 5 s default.
- **A test waits for an outcome, never for the runner.** A test file imports a lazily loaded surface's module before it
  renders the surface, so `React.lazy` resolves from the module cache instead of waiting on a cold transform of the
  chunk. A debounced input takes its delay as a prop that tests set to `0` (`TenantsPage`'s `addressPreviewDelayMs`),
  so a test never waits out a real debounce. A test awaits the request mock or the settled UI, never a duration.
  Testing Library's `waitFor` recognizes only Jest's fake timers, so Vitest's fake timers stall it and MUI transitions
  and are not an option (issue [#302](https://github.com/NCodeGroup/Identity/issues/302)).
- **The interactive bundle mounts over the page the host rendered.** `mountSurface`
  ([`apps/auth/src/mount.tsx`](apps/auth/src/mount.tsx)) runs when the bundle's module runs and fetches nothing: it
  reads the `#branding` block and the page's state with `readServerPage`
  ([`apps/auth/src/serverPage.ts`](apps/auth/src/serverPage.ts)), which holds the values of the text, email and password
  inputs by id, the `scope` checkboxes the end-user cleared, the id of the focused input, and the authorization error
  (`#authorization-error`, its `data-error-code`, `#authorization-error-description`), then calls
  `createRoot(container).render(...)`. Hydration does not apply, because the host's markup is not React's output.
  `writeCarriedValue` carries a value into an input from a stable ref callback: it assigns the DOM `value` property,
  never the attribute that React's `defaultValue` writes, so a carried password stays out of the markup. Never call
  `flushSync` here (`@eslint-react/dom-no-flush-sync` is an error); a root's first render is not time-sliced, so it
  is not needed (ADR-0253).
- **The interactive bundle carries the page's Content-Security-Policy nonce.** `defineBackendServedSurfaceConfig` sets
  Vite's `html.cspNonce` to `CSP_NONCE_PLACEHOLDER`, so the build writes `nonce="__NCODE_CSP_NONCE__"` on every script
  and modulepreload link and on a `<meta property="csp-nonce">`; the host writes the response's nonce in its place, and
  `mountSurface` reads it with `readCspNonce` and gives it to the emotion cache, so MUI's `<style>` elements are
  allowed. A browser hides the `nonce` attribute once the page is parsed, so read the `nonce` property; jsdom has no
  such property, so a test assigns `meta.nonce` itself (ADR-0256).
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
- **A test that a superseded async result is dropped must settle it last.** When a container abandons one load for
  another, settle the current load first and the abandoned one afterwards (inside `act`), then assert the state still
  holds the current result. Settling the abandoned one first passes even without the guard, because the current
  result overwrites it. To prove an aborted failure is ignored, assert while the replacement load is still pending:
  once it resolves, its result hides the error. [`BootstrapGate.test.tsx`](packages/shell/src/BootstrapGate.test.tsx)
  shows both. Check such a test by removing the guard and watching the test fail.
- **Management writes are JSON Patch + `If-Match`, not whole-object PUT.** The Management update endpoints
  bind `JsonPatchDocument<T>` and hydrate the current resource before applying the document, and they gate
  on an `If-Match` concurrency token (optimistic concurrency). RTK Query mutations express an edit as a
  `replace`-only patch built from the stable wire property names via the UI-free
  [`replacePatch`](packages/api/src/jsonPatch.ts) helper (`/displayName`, `/issuer`, …), and send the
  resource's `concurrencyToken` as the `If-Match` header. Creates are plain `POST` (the id is
  server-generated, except a tenant's, which its creator chooses; the response carries the new
  `concurrencyToken`), deletes are `DELETE` (a `409` means
  the resource still has dependents). Invalidate both the `LIST` tag and the per-id tag so the scoped
  caches reconcile. Server failures are RFC 7807 Problem Details; surface them through
  [`apiErrorMessage`](packages/store/src/apiError.ts) rather than re-deriving messages in each container.
- **A resource every owner has is one endpoint per operation, keyed by a `ManagementOwner`.** Secrets and settings
  belong to the server, a tenant, or a client or resource server in a scope; `@ncode/api`'s
  [`owner.ts`](packages/api/src/owner.ts) builds each owner's API path (`ownerApiPath`) and cache tags (`ownerTagId`,
  `ownerEffectiveTagId`), and `@ncode/store` defines `listSecrets`, `getSettings` and the rest once. A new owner kind is
  a member of `ManagementOwner`, never another set of endpoints; the detail pages render `OwnerSecretsTab` and
  `OwnerSettingsTab` (ADR-0257).
- **A query that checks live input shows its result only while the result describes that input.** RTK Query keeps
  returning the previous `data` while a query for new arguments loads, and a debounced input lags the field it reads,
  so a preview of the old input would be shown as the current one. Compare the hook's `originalArgs` with the input as
  it is now and drop the result when they differ, as the tenant form's address preview in
  [`TenantsPage`](apps/mgmt/src/containers/TenantsPage.tsx) does. Turn such a query off with the `{ skip }` option:
  apps reach RTK Query only through `@ncode/store` and do not depend on `@reduxjs/toolkit`, so `skipToken` is not
  importable there.
- **A `data-testid` on a MUI control lands on the root, not the native input.** `<Switch data-testid=…>`
  (and friends) forward the attribute to the outer element, so `getByTestId(...)` returns a `<span>` and
  matchers like `.toBeChecked()` fail. `TextField`'s `slotProps.htmlInput` _does_ reach the input (its
  type has an index signature), but `Switch`'s `slotProps.input` type rejects `data-*`.
  Prefer querying the control by role and accessible name instead —
  `getByRole('switch', { name: 'Disabled' })` (the `FormControlLabel` supplies the name) — which is both
  type-safe and closer to how a user finds it. Use the control's real role: since MUI 9 a `Switch` is
  `role="switch"`, not `checkbox`, and only a `Checkbox` answers to `'checkbox'`.
- **Drive the UI through Testing Library, not native DOM calls.** `fireEvent.click(element)` (and `userEvent`) run
  inside React's `act`, so the update they trigger is flushed before the next assertion. A native `element.click()`
  runs outside it, and the resulting state update logs a _"not wrapped in act(...)"_ warning — which the console
  guard turns into a failure. When a test asserts that **nothing** changes after a promise settles (a rejected load
  that leaves the loading state in place), there is no UI state for `findBy…` to wait on; flush with
  `await act(async () => {})` and then assert.
- **In Vitest 5, `mockRestore()` also clears a spy's call history.** Assert on `spy.mock.calls` (or
  `toHaveBeenCalledWith`) **before** restoring the spy; afterwards the spy reports zero calls.
- **MUI has no system props; style through `sx`.** MUI 9 removed the system-prop shorthands from `Box`, `Stack`,
  `Typography`, `Link`, and `Grid` (`<Box display="flex" py={2}>`, `<Typography color="text.secondary">`), and the
  deprecated `*Props` escape hatches (`inputProps`, `InputProps`, `PaperProps`, …) in favor of `slots` / `slotProps`
  (`slotProps={{ htmlInput: { … } }}` on a `TextField`, `slotProps={{ input: … }}` on a `Checkbox` / `Switch`). A
  removed prop is a type error, so `pnpm typecheck` catches it. To migrate code in bulk, use MUI's codemods
  (`npx @mui/codemod@<version> v9.0.0/system-props <path>` and `deprecations/all`). Pin the version: 9.5.0's CLI is
  broken (`yargs.command is not a function`), so use 9.4.0. Run them on LF checkouts and re-run Prettier afterwards,
  because jscodeshift writes CRLF on Windows and expands every object it touches.
- **Test type entry points are the Vitest ones.** Vitest no longer pulls Jest's global matcher namespace or
  `@types/node` into a test program, so name them explicitly: a tsconfig lists `@testing-library/jest-dom/vitest` in
  `types` (the bare `@testing-library/jest-dom` entry augments Jest only, and `toBeInTheDocument` stops type-checking);
  a `vite.config.ts` with a `test` block starts with `/// <reference types="vitest/config" />` (the bare `vitest`
  reference no longer types that key — configs are not in any tsconfig, so only the editor shows it); and a jsdom app
  whose tests import `node:` modules lists `"node"` in `types` (as [`apps/playground`](apps/playground) does for
  `webcrypto`, which jsdom lacks).
- **`@types/node` is pinned to the runtime major workspace-wide.** The root `devDependency` is the one range; an
  `overrides` entry in [`pnpm-workspace.yaml`](pnpm-workspace.yaml) (`'@types/node': '$@types/node'`) forces every
  transitive and auto-installed copy to it — without it, `auto-install-peers` resolves Vite's and Vitest's
  `@types/node` peer to the newest major in every workspace that does not declare it. Dependabot ignores its majors;
  move it only together with `.node-version` and `engines.node`
  ([ADR-0152](../../docs/decisions/0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md)).
- **A dependency with an install script fails `pnpm install` until it is decided.** `strictDepBuilds` turns pnpm's
  "Ignored build scripts" warning into `ERR_PNPM_IGNORED_BUILDS`. Decide whether the script is needed and record it in
  `allowBuilds` in [`pnpm-workspace.yaml`](pnpm-workspace.yaml) (`true` to run it, `false` to skip it); prefer
  `false` unless the package cannot work without it. pnpm keeps a skipped script in `node_modules/.modules.yaml` for
  any package it does not rebuild, so a `node_modules` installed before the decision keeps failing; delete it and
  reinstall.
- **A version younger than three days is refused.** `minimumReleaseAge` makes `pnpm add` and `pnpm install` resolve only
  versions published at least three days ago; asking for a newer one fails with `ERR_PNPM_NO_MATURE_MATCHING_VERSION`.
  The window equals Dependabot's npm `cooldown` and must never be shorter: Dependabot enforces its cooldown on every
  version already in the lockfile, so its npm update fails with that same error while a younger version is locked, and
  passes again once the version ages. When a fix cannot wait (and is not arriving as a Dependabot security update,
  which bypasses both gates), list the package in `minimumReleaseAgeExclude` with a comment and remove the entry once
  the version is three days old
  ([ADR-0153](../../docs/decisions/0153-one-release-age-window-gates-frontend-dependencies.md)). The gate also re-checks the
  locked versions a non-frozen install re-resolves, so adding a workspace or a dependency can move a locked transitive
  version younger than three days back to an older one. Review the lockfile diff for such unrelated moves. When a change
  adds no external version (a new workspace, or a workspace dependency), keep the locked versions by running that one
  install with `--config.minimum-release-age=0` and confirm the diff only adds importers.
- **A non-interactive `pnpm install` cannot replace `node_modules` on its own.** After a pnpm major or a store-layout
  change, pnpm must purge `node_modules` first and asks for confirmation; without a terminal (an agent's shell, a script
  run from another tool) it fails with `ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY`. Run
  [`build/frontend-install.ps1`](../../build/frontend-install.ps1), which passes `--config.confirm-modules-purge=false`,
  or pass that flag yourself.
- **Force an uncached run with `TURBO_FORCE=true`, not `--force`.** Turborepo replays a cached task's output, so a
  re-run after a runtime or toolchain change can report results it never recomputed. The scripts end in `--` for
  Vitest (`test:report` is `turbo run test … -- --coverage …`), so a `--force` appended to `pnpm run test:report` goes
  to Vitest, not Turborepo; set the environment variable instead (`$env:TURBO_FORCE = 'true'` in pwsh).
- **Lint is one flat config at the workspace root.** [`eslint.config.js`](eslint.config.js) is found by walking up from
  each linted file, so a package's `lint` script needs no config of its own. React rules come from
  `@eslint-react/eslint-plugin` (`eslint-plugin-react` does not support ESLint 10), and `eslint-plugin-react-hooks` adds
  the React Compiler checks; `@eslint-react`'s `disable-conflict-eslint-plugin-react-hooks` preset keeps the overlap
  reported once. Its naming rules expect refs to end in `Ref` and a `useState` setter to be `set` + the capitalized
  state name (`[isBusy, setIsBusy]`).
- **A Node/backend CLI app (not a browser bundle) drops the DOM lib and runs via `vite-node`.** A headless
  app such as [`apps/conformance-runner`](apps/conformance-runner) sets `lib: ["ES2022"]` + `types: ["node"]`
  in its `tsconfig.json` (overriding the base, which adds `DOM`) so the global `fetch`/`Response`/`Buffer`/
  `process` come from `@types/node`, not from two conflicting lib declarations. It has no Vite bundle; `build`
  is type-check only (`tsc --noEmit`) like a package, and it runs from TypeScript source via **`vite-node`**
  (`"start": "vite-node src/main.ts --"`), which resolves workspace `@ncode/*` packages as source — plain
  `node` cannot, because those packages' `main` points at `./src/index.ts`. The trailing `--` in the `start`
  script lets `vite-node` forward CLI args to the program; the arg parser ignores a stray `--`.
