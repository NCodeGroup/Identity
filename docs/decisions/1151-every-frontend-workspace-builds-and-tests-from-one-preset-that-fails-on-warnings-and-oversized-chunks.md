# 1151. Every frontend workspace builds and tests from one preset that fails on warnings and oversized chunks

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The frontend workspace ([ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md))
runs Vitest in every workspace, and CI collects their coverage
([ADR-1143](1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md)).
A green run has to claim what it appears to:

- **Warnings are noise unless they fail.** A React "An update to ... inside a test was not wrapped in act(...)" warning
  is a test asserting before an async update has settled, so the test can pass for the wrong reason, and a new warning
  is buried in the existing ones.
- **Vitest strips types without checking them.** A workspace that excludes its tests or stories from `tsc` never fails
  on a type error in them.
- **Lint warnings accumulate.** Without `--max-warnings`, a stale `eslint-disable` comment is only a warning.
- **Coverage must measure every file and hold a floor.** A glob that matches only `*.tsx` hides a theme factory, and a
  test glob that matches only `*.test.tsx` never runs a `.test.ts` file.
- **Configuration copies drift.** If each workspace carries its own Vite and Vitest configuration, the test globs, the
  serving rules and the gates diverge, and the configuration files sit outside every lint and type-check gate.
- **Turborepo hashes a task from its own package's files**, its `package.json`, the lockfile and its `turbo.json`.
  A change to an internal package a workspace depends on, or to a root file every workspace reads, leaves the
  dependents' `test` and `typecheck` hashes unchanged, and turbo replays their cached results locally.
- **A single bundle is slow and uncacheable.** When React, MUI, emotion, Redux and every surface's containers land in
  one entry chunk over 500 kB, a browser downloads and parses the whole console before it paints, and any application
  change invalidates the cached copy of every library. Vite only warns about chunk size, and a warning in a passing
  build goes unread.

## Decision

**One workspace package, `@ncode/vite-config`, owns every Vite and Vitest configuration, and each workspace's
configuration file is a single call to it with that workspace's coverage floors. The test gates fail on any warning,
and the build fails on any oversized chunk.**

### The preset

1. **One factory per kind of workspace.**
   - `defineTestConfig({ environment, coverage })` holds the test gates for a workspace that only tests: a package or
     the headless runner. `environment` is Vitest's own open vocabulary. A browser-simulating environment adds
     `BROWSER_TEST_TIMEOUT_MS` (30 s) and Vitest globals.
   - `defineStandaloneSurfaceConfig({ port, bootstrapHost, coverage })` serves a surface from its own dev server. The
     port is pinned with `strictPort` (Playground 5174, conformance 5175), and only `/config/bootstrap.json` is proxied,
     to the Aspire service the `bootstrapHost` names, falling back to a fixed origin outside Aspire. The dev server
     serves over HTTPS as [ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md)
     describes. `@ncode/config`'s tests check that the proxied path is the one `loadRuntimeConfig` fetches.
   - `defineBackendServedSurfaceConfig({ coverage })` builds a bundle the OpenID Provider host serves, with base `./`;
     the host sets the document's `<base href>` to where it serves the bundle
     ([ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)).
   - `defineBffServedSurfaceConfig({ coverage })` builds a bundle a BFF serves, with base `/`; `apps/mgmt` uses it and
     has no dev server of its own.
   - The two served factories set `html.cspNonce` to `CSP_NONCE_PLACEHOLDER`, which the serving host replaces per
     response.
2. **Node loads the preset as TypeScript by stripping types.** Vite bundles a configuration file but leaves its bare
   imports external, so `src/preset.ts` is one module with erasable syntax only (`erasableSyntaxOnly`), no relative
   imports (they would need `.ts` specifiers) and no runtime imports of other workspace packages (their `.js`
   specifiers Node cannot map). The preset validates its own options (coverage floors, port, base, bootstrap host),
   because the files that call it are not type-checked.
3. **`@ncode/test-support` keeps its own configuration**, because turbo refuses a cyclic workspace graph and the preset
   needs the console guard for its own tests. Every workspace keeps its own `vitest.setup.ts`, so the console guard
   registers its hooks on the Vitest copy that runs the suite.

### Test and lint gates

4. **A test that logs `console.error` or `console.warn` fails.** `failOnConsole` from `@ncode/test-support`, which every
   workspace registers from its `vitest.setup.ts`, records the guarded methods during each test, still forwards the
   output, and throws from `afterEach` with every recorded message. The only exemption is Redux Toolkit's
   development-mode timing notice on `console.warn` (its immutability or serializability check took longer than 32 ms),
   which depends on machine load, not on the code; the checks' actual findings are `console.error` and still fail. The
   exemption list is for timing diagnostics only. A test whose behavior is logging mocks the method
   (`vi.spyOn(console, 'error').mockImplementation(...)`) and asserts on the spy.
5. **Lint warnings are errors.** Every `lint` script passes `--max-warnings 0`, and the root `eslint.config.js` reports
   unused disable directives and unused inline configs as errors.
6. **A test that makes no assertion fails.** Every Vitest config sets `expect.requireAssertions`.
7. **Every test, story and setup file is type-checked.** No workspace tsconfig excludes `*.test.*` or `*.stories.*`, and
   each includes its `vitest.setup.ts`.
8. **Coverage measures every source file and holds a floor.** Coverage includes every `.ts` and `.tsx` source except the
   entry point (`main.ts` or `main.tsx`) and the re-export barrel (`index.ts`). Each workspace sets
   `coverage.thresholds` for statements, branches, functions and lines at the level it reached, rounded down.
   `pnpm run test:report` enforces them whenever coverage is collected, which CI always does; a plain `pnpm test` stays
   fast. A change that raises coverage raises the floor in the same change; lowering a floor needs a stated reason.

### Turbo hashing

9. **Turbo hashes what a task reads.** A no-op `transit` task (`dependsOn: ["^transit"]`) links the package graph;
   `test` and `typecheck` depend on it, so their hashes include their internal dependencies' files while the tasks
   still run in parallel. `globalDependencies` adds `tsconfig.base.json` and `eslint.config.js` to every hash. CI keeps
   no turbo cache.

### Chunks

10. **Vendor code splits into fixed chunks.** `VENDOR_CHUNKS` lists the groups, highest priority first: `react`
    (`react`, `react-dom`, `scheduler`), `mui` (`@mui/*`, `@emotion/*`), `redux` (`@reduxjs/toolkit`, `redux`,
    `react-redux` and their helpers) and `vendor` (every other package). The preset passes them to Rolldown's
    `output.codeSplitting.groups`. A vendor chunk changes only when its packages are upgraded, so it stays cached across
    application releases.
11. **Surface containers load lazily.** `apps/mgmt`, `apps/playground` and `apps/conformance` import their route-level
    containers through `React.lazy` inside a `Suspense` boundary: one chunk per surface in the management console, and
    the single container as its own chunk in the playground and the conformance dashboard. The shell paints before a
    surface loads, and a surface never visited is never downloaded. A test that asserts on a lazily loaded container
    waits for it with `findBy...`.
12. **An oversized chunk fails the build.** The `failOnOversizedChunks` plugin checks every emitted code chunk in
    `generateBundle` against the build's `chunkSizeWarningLimit`, set to `CHUNK_SIZE_LIMIT_KB` (500). It fails the build
    and names each offending chunk. The plugin reads the same limit Vite warns at, so there is one threshold. The fix is
    to split further, never to raise the limit.

### Adding a workspace

13. Adding a workspace means `@ncode/test-support` as a `devDependency`, a `vitest.setup.ts` that calls
    `failOnConsole`, `--max-warnings 0` on its `lint` script, and a preset factory call with its coverage floors.

## Options considered

- **`vitest-fail-on-console` (npm).** Rejected: it adds a third-party dependency, tied to its own Vitest peer range,
  for a few dozen lines the repository can own and test fully.
- **Vitest's `onConsoleLog` hook.** Rejected: it can filter or silence output but cannot fail the test that produced it.
- **A per-file coverage threshold.** Rejected: a workspace floor stops regressions without forcing tests onto trivial
  re-exports.
- **A uniform 100% threshold.** Rejected: some branches are defensive, such as an aborted request racing a rejection,
  and a blanket 100% pushes tests toward implementation details instead of behavior.
- **A shared configuration file at the workspace root, imported by relative path.** Rejected: it is a deep import across
  package boundaries ([ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md)),
  and the file sits outside lint, type-checking and tests.
- **A multi-file preset with `.ts` import specifiers.** Rejected: any workspace whose type-check reaches the preset
  needs `allowImportingTsExtensions`, putting two specifier styles in one codebase.
- **Loading configuration through Vite's module runner (`--configLoader runner`).** Rejected: every `vite` and `vitest`
  invocation would need the flag.
- **A Node-typed `tsconfig.node.json` per workspace.** Rejected: it adds a project per workspace to check one call; the
  preset carries the types and validates the values at run time.
- **`dependsOn: ["^test"]` and `["^typecheck"]`.** Rejected: correct, but it serializes the graph, and the transit task
  gets the same hashes without that cost.
- **Per-task `$TURBO_ROOT$` inputs instead of `globalDependencies`.** Rejected: they invalidate fewer tasks, but each
  task must restate `$TURBO_DEFAULT$`, and the root configuration rarely changes.
- **Raising `chunkSizeWarningLimit`.** Rejected: it hides the problem without fixing it.
- **One preset that fails on warnings and oversized chunks (chosen).** A gate, a glob, a serving rule or a build limit
  changes in one place for every workspace.

## Consequences

- A `console.error` or `console.warn`, a lint warning, an assertion-free test, a coverage drop below a floor and an
  oversized chunk each fail CI at the step that runs them, not later in a report.
- Every workspace collects and measures `.tsx` tests and sources, and the preset's own options are validated at run
  time.
- Editing a shared package re-runs its dependents' `test` and `typecheck` locally, and editing the root TypeScript or
  ESLint configuration re-runs every task.
- Adding a dependency or a surface that pushes any chunk past 500 kB fails `pnpm build`; the author splits it with a
  vendor group or a lazy boundary.
- Adding a workspace means calling a preset factory instead of copying a configuration.

## References

- [`frontend/packages/vite-config`](../../frontend/packages/vite-config),
  [`frontend/packages/test-support`](../../frontend/packages/test-support),
  [`frontend/turbo.json`](../../frontend/turbo.json), [`frontend/eslint.config.js`](../../frontend/eslint.config.js),
  each workspace's `vitest.setup.ts` and `vite.config.ts` / `vitest.config.ts`
- [ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md) (frontend
  platform), [ADR-1143](1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md)
  (CI test and coverage reports),
  [ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md)
  (the AppHost and service discovery)
- Vitest: [coverage thresholds](https://vitest.dev/config/#coverage-thresholds); React:
  [`act`](https://react.dev/reference/react/act)
- Turborepo: "Dependent tasks that can be run in parallel" (transit nodes), `globalDependencies`
- Node.js: [type stripping](https://nodejs.org/api/typescript.html#type-stripping)
- Issues [#149](https://github.com/NCodeGroup/identity/issues/149), [#150](https://github.com/NCodeGroup/identity/issues/150),
  [#152](https://github.com/NCodeGroup/identity/issues/152)
