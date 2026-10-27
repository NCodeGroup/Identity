# 175. Bundles split into vendor and surface chunks, and an oversized chunk fails the build

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

`pnpm build` warned that `apps/mgmt`, `apps/playground`, and `apps/conformance` each emitted one chunk larger than
500 kB after minification. React, MUI, emotion, Redux, and every surface's containers all landed in a single entry
chunk. A browser had to download and parse the whole console before it could paint anything, and any application change
invalidated the cached copy of every library.

Vite only warns about chunk size, and a warning in a passing build goes unread.
[ADR-0151](0151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md) already makes the test gates treat
warnings as errors. Raising `build.chunkSizeWarningLimit` would hide the problem without fixing it.

## Decision

The shared preset ([ADR-0157](0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md)) sets the build
options for every surface, so the rules below apply to every bundle and live in one place.

- **Vendor code splits into fixed chunks.** `VENDOR_CHUNKS` lists the code-splitting groups, highest priority first:
  `react` (`react`, `react-dom`, `scheduler`), `mui` (`@mui/*`, `@emotion/*`), `redux` (`@reduxjs/toolkit`, `redux`,
  `react-redux`, and their helpers), and `vendor` (every other package). The preset passes them to Rolldown's
  `output.codeSplitting.groups`, which replaces the deprecated `manualChunks` in Vite 8. A vendor chunk changes only
  when its packages are upgraded, so it stays cached across application releases.
- **Surface containers load lazily.** Each app's `App.tsx` imports its route-level containers through `React.lazy`
  inside a `Suspense` boundary. For the management console that means one chunk per surface. For the playground and the
  conformance dashboard, the single container is its own chunk. The shell paints before the surface loads, and a surface
  that is never visited is never downloaded.
- **An oversized chunk fails the build.** The `failOnOversizedChunks` plugin checks every emitted code chunk in
  `generateBundle` against the build's `chunkSizeWarningLimit` (500 kB, Vite's default, set explicitly as
  `CHUNK_SIZE_LIMIT_KB`). It fails the build and names each offending chunk. The plugin reads the same limit Vite warns
  at, so there is one threshold, not two. The fix it points to is splitting further, not raising the limit.

## Consequences

- The largest chunk is now the MUI vendor chunk, about 310 kB minified in the management console, well under the limit.
- Adding a dependency or a surface that pushes any chunk past 500 kB fails `pnpm build` in CI. The author must split it
  by adding a vendor group or a lazy boundary.
- A test that asserts on a lazily loaded container must wait for it (`findBy…`) rather than query synchronously.
