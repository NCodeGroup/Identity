# 151. Frontend tests treat warnings as errors and hold coverage floors

- **Status:** Accepted (amended by
  [ADR-0157](0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md): the gates live in the shared
  `@ncode/vite-config` preset, and each workspace states only its coverage floors)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

The frontend workspace ([ADR-0064](0064-frontend-presentation-platform.md)) runs Vitest in eleven workspaces, and CI
collects their coverage ([ADR-0143](0143-ci-presents-test-results-and-coverage-in-github.md)). An audit of a green run
found that "green" claimed less than it appeared to:

- **Warnings were noise, not failures.** A passing `pnpm test` logged eight React "An update to … inside a test was not
  wrapped in act(...)" warnings (issue [#149](https://github.com/NCodeGroup/Identity/issues/149)). Each one is a test asserting before an async update has settled, so it
  can pass for the wrong reason, and any new warning was buried in the existing ones.
- **Seven of eleven workspaces excluded their tests from `tsc`.** Vitest strips types without checking them, so a type
  error in a test or a Storybook story never failed anything.
- **Lint warnings could accumulate.** No workspace passed `--max-warnings`, and a stale `eslint-disable` comment is only
  a warning by default.
- **Coverage did not measure every file.** `@ncode/ui` measured only `*.tsx`, so its theme factory was invisible, and
  its test glob matched only `*.test.tsx`, so a `.test.ts` file there would never have run. Nothing stopped coverage
  from falling: several files sat at 0% (each app's `BootstrapGate`, the store's typed hooks, two `@ncode/ui`
  components) and the RTK Query endpoint definitions were half covered.

## Decision

1. **A test that logs `console.error` or `console.warn` fails.** The `@ncode/test-support` workspace package provides
   `failOnConsole`, which every workspace registers from its `vitest.setup.ts`. It records the guarded methods during
   each test, still forwards the output, and throws from `afterEach` with every recorded message. A test whose
   behavior _is_ logging opts out locally by mocking the method (`vi.spyOn(console, 'error').mockImplementation(…)`)
   and asserting on the spy. The setup injects Vitest's `beforeEach`/`afterEach` into the guard, so the package has no
   runtime dependency on `vitest` and cannot register hooks on a different copy than the one running the suite. Its
   message formatter is dependency-free so that every workspace, including the browser-typed ones without Node's
   `util`, type-checks the setup. The guard drops one message, and only through `console.warn`: Redux Toolkit's
   development-mode notice that its immutability or serializability check took longer than 32 ms. Whether that fires
   depends on machine load under v8 coverage (a full coverage run logged it ten times), not on the code, so failing on
   it would make store-backed tests flaky and printing it would only rebuild the noise; the checks' actual findings (a
   mutated or non-serializable value) are `console.error` and still fail. The exemption list is for timing diagnostics
   only.
2. **Lint warnings are errors.** Every workspace's `lint` script passes `--max-warnings 0`, and the root
   `eslint.config.js` reports unused disable directives and unused inline configs as errors.
3. **A test that makes no assertion fails.** Every Vitest config sets `expect.requireAssertions`, so a test cannot pass
   merely by executing code.
4. **Every test, story, and setup file is type-checked.** No workspace tsconfig excludes `*.test.*` or `*.stories.*`,
   and each includes its `vitest.setup.ts`.
5. **Coverage measures every source file and holds a per-workspace floor.** Each workspace's coverage `include` covers
   all `.ts`/`.tsx` sources; only the entry point (`main.ts`/`main.tsx`) and the re-export barrel (`index.ts`) are
   excluded. Each Vitest config sets `coverage.thresholds` for statements, branches, functions, and lines at the
   level the workspace has reached, rounded down. Vitest enforces thresholds whenever coverage is collected, which CI
   always does (`pnpm run test:report`); a plain `pnpm test` stays fast. A change that raises coverage raises the
   floor in the same change; lowering a floor needs a stated reason in the change.

## Options considered

- **`vitest-fail-on-console` (npm).** Not adopted: it adds a third-party dependency, tied to its own Vitest peer range,
  for a few dozen lines that the repository can own and test fully.
- **Vitest's `onConsoleLog` hook.** Rejected: it can filter or silence output but cannot fail the test that produced
  it.
- **A per-file threshold (`thresholds.perFile`).** Not adopted now: a workspace floor stops regressions without
  forcing tests onto trivial re-exports. It can be revisited once every file is near its workspace's level.
- **A uniform 100% threshold.** Rejected: some branches are defensive (for example an aborted request racing a
  rejection) and a blanket 100% would push tests toward exercising implementation details instead of behavior.

## Consequences

- The eight `act(...)` warnings of issue [#149](https://github.com/NCodeGroup/Identity/issues/149) are fixed, and any new one fails its own test with the message that
  explains it.
- Closing the gaps raised frontend line coverage from 81% to 99% (1,713 of 2,114 lines to 2,172 of 2,195) and branch
  coverage from 74% to 98%; eight of the twelve workspaces are at 100%. What remains uncovered is defensive code the UI
  cannot reach, such as a submit handler's `canSubmit` check behind a button disabled under the same condition.
- The new tests exposed four defects, now fixed: the Management console parsed the access token (which a client must
  treat as opaque) to find the user, so an opaque token broke sign-in and renewal; the conformance dashboard could show
  a superseded load's audit under a newer issuer; a network error on the conformance runner's device-code poll aborted
  the whole run instead of failing that probe; and the runner accepted RSA-PSS-restricted keys it could not publish as a
  JWK. Making the runner's CLI testable also moved its orchestration out of the entry point into `cli.ts`.
- A workspace-wide refactor that drops coverage fails CI at the `Test` step rather than being noticed in Codecov later.
- Build output is not yet held to the same standard. Turbo's "no output files" warning for the conformance runner's
  type-check-only build is gone (its package `turbo.json` declares no outputs), but Vite still warns that three SPA
  bundles exceed 500 kB. Splitting them and then failing the build on that warning is issue [#152](https://github.com/NCodeGroup/Identity/issues/152); raising the limit
  would only hide it.
- Adding a workspace means adding `@ncode/test-support` as a `devDependency`, a `vitest.setup.ts` that calls
  `failOnConsole`, `--max-warnings 0` on its `lint` script, and `expect.requireAssertions` plus coverage thresholds in
  its Vitest config — the [frontend README](../../src/frontend/README.md) lists the steps.

## References

- [`src/frontend/packages/test-support`](../../src/frontend/packages/test-support),
  [`src/frontend/eslint.config.js`](../../src/frontend/eslint.config.js), each workspace's `vitest.setup.ts` and
  `vite.config.ts` / `vitest.config.ts`
- [ADR-0064](0064-frontend-presentation-platform.md) (frontend platform),
  [ADR-0143](0143-ci-presents-test-results-and-coverage-in-github.md) (CI test and coverage reports),
  [ADR-0147](0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md) (frontend toolchain)
- Vitest: [coverage thresholds](https://vitest.dev/config/#coverage-thresholds)
- React: [`act`](https://react.dev/reference/react/act)
