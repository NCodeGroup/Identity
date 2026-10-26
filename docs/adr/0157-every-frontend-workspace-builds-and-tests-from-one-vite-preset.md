# 157. Every frontend workspace builds and tests from one Vite preset

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

Each frontend workspace carried its own copy of the Vite and Vitest configuration. The copies held the test gates of
[ADR-0151](0151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md), and each standalone surface also
held the development proxy for its bootstrap document. They had drifted:

- **The test globs differed.** The workspaces on the `node` environment collected only `*.test.ts` and measured only
  `*.ts`, so a `.test.tsx` there would never run. ADR-0151 had found and fixed this same defect in `@ncode/ui` alone.
- **The serving rules differed.** The Playground and conformance dashboard pinned their dev-server ports with
  `strictPort`, but the Management console did not. Its seeded redirect URI, the developer CORS allowlist, and the
  Aspire AppHost all assume port 5173, and outside Aspire, Vite would have moved it to the next free port, which is the
  Playground's.
- **The configuration files were outside every gate.** No workspace type-checks or lints its `vite.config.ts` or
  `vitest.config.ts`.

Turborepo hashes a task from its own package's files, `package.json`, the lockfile, and its `turbo.json`. Changing an
internal package that a workspace depends on, or a root file every workspace reads (`tsconfig.base.json`,
`eslint.config.js`), left the dependents' `test` and `typecheck` hashes unchanged. Turbo then replayed their cached
results locally, which a shared preset would make true of every workspace.

## Decision

**One workspace package, `@ncode/vite-config`, owns the Vite and Vitest configuration, and every other workspace's
configuration file is a single call to it with that workspace's coverage floors.**

1. **The preset has one factory per kind of workspace.**
   - `defineTestConfig({ environment, coverage })` holds the ADR-0151 gates for a workspace that only tests: a package
     or the headless runner. The gates are the setup file, `requireAssertions`, and one set of test and coverage
     globs. Coverage excludes only tests, stories, the entry point, and the barrel. A browser-simulating environment
     adds the jsdom test timeout and Vitest globals. `environment` is Vitest's own open vocabulary.
   - `defineStandaloneSurfaceConfig({ port, bootstrapHost, coverage })` serves a surface from its own dev server. The
     port is pinned with `strictPort`, and the bootstrap document is proxied to the Aspire service the AppHost
     references, falling back to a fixed origin outside Aspire.
   - `defineBackendServedSurfaceConfig({ coverage })` builds a bundle the OpenID Provider host serves. The bundle has
     a relative base; the host sets the document's `<base href>` to where it serves the bundle
     ([ADR-0193](0193-the-interactive-surface-is-served-under-the-tenant-route.md)).
2. **Node loads the preset directly.** Vite bundles a configuration file but leaves its bare imports external, so Node
   imports the preset as TypeScript source by stripping its types. The preset is therefore one module
   (`src/preset.ts`):
   - It uses erasable syntax only, enforced by `erasableSyntaxOnly`.
   - It has no relative imports, which would need `.ts` specifiers.
   - It has no runtime imports of other workspace packages, which use `.js` specifiers Node cannot map.
   - It validates its own options (coverage floors, port, base, bootstrap host), because the files that call it are
     not type-checked.
   `@ncode/config`'s tests check the one contract the preset repeats: the dev servers proxy the path `loadRuntimeConfig`
   fetches.
3. **`@ncode/test-support` keeps its own configuration.** The preset needs the console guard for its own tests, and
   turbo rejects a cyclic workspace graph. Its configuration states the same gates and says to keep them in step.
   Every workspace keeps its own `vitest.setup.ts`, because the guard must register its hooks on the Vitest copy that
   runs the suite (ADR-0151).
4. **Turbo hashes what a task reads.** A no-op `transit` task (`dependsOn: ["^transit"]`) links the package graph.
   `test` and `typecheck` depend on it, so their hashes include their internal dependencies' files while the tasks
   still run in parallel. `globalDependencies` adds `tsconfig.base.json` and `eslint.config.js` to every hash.

## Options considered

- **A shared configuration file at the workspace root, imported by relative path.** Rejected: it is a deep import
  across package boundaries ([ADR-0064](0064-frontend-presentation-platform.md)), and the file would sit outside lint,
  type-checking, and tests.
- **A multi-file preset with `.ts` import specifiers.** Rejected: any workspace whose type-check reaches the preset
  would need `allowImportingTsExtensions`, putting two specifier styles in one codebase.
- **Load configuration through Vite's module runner (`--configLoader runner`).** Rejected: every `vite` and `vitest`
  invocation, in every script and by hand, would need the flag.
- **Type-check each configuration file through a Node-typed `tsconfig.node.json` per workspace.** Rejected: it adds a
  project per workspace to check one call. The preset carries the types and validates the values at run time.
- **`dependsOn: ["^test"]` and `["^typecheck"]`.** Rejected: correct, but it serializes the graph, and the transit task
  gets the same hashes without that cost.
- **Per-task `$TURBO_ROOT$` inputs instead of `globalDependencies`.** Not chosen: they invalidate fewer tasks, but each
  task must restate `$TURBO_DEFAULT$`, and the root configuration rarely changes.

## Consequences

- A gate, a glob, or a serving rule changes in one place for every workspace. Splitting the standalone bundles (issue
  [#152](https://github.com/NCodeGroup/Identity/issues/152)) lands once in the preset.
- Every workspace collects and measures `.tsx` tests and sources, and the Management console refuses to start on any
  port but 5173.
- Editing a shared package re-runs its dependents' `test` and `typecheck` locally, and editing the root TypeScript or
  ESLint configuration re-runs every task. CI keeps no turbo cache, so it is unaffected.
- Adding a workspace means calling a preset factory instead of copying a configuration.
- Amends [ADR-0151](0151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md): the gates it decided live
  in the preset, and each workspace states only its floors.

## References

- [`src/frontend/packages/vite-config`](../../src/frontend/packages/vite-config),
  [`src/frontend/turbo.json`](../../src/frontend/turbo.json), each workspace's `vite.config.ts` / `vitest.config.ts`
- [ADR-0151](0151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md) (the test gates),
  [ADR-0064](0064-frontend-presentation-platform.md) (package boundaries),
  [ADR-0071](0071-aspire-local-dev-orchestrator.md) (the AppHost and service discovery),
  [ADR-0156](0156-shared-app-shell-containers-live-in-ncode-shell.md) (the shared shell)
- Turborepo: "Dependent tasks that can be run in parallel" (transit nodes), `globalDependencies`
- Node.js: [type stripping](https://nodejs.org/api/typescript.html#type-stripping)
- Issue [#150](https://github.com/NCodeGroup/Identity/issues/150)
