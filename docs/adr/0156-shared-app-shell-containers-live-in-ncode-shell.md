# 156. Shared app-shell containers live in `@ncode/shell`

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

The frontend platform ([ADR-0064](0064-frontend-presentation-platform.md)) layers its packages so each is testable on
its own: `@ncode/ui` holds stateless presentational components and depends on no store, `@ncode/store` and
`@ncode/config` hold no UI, and the stateful containers that wire them live in each app.

Every standalone surface (`apps/mgmt`, `apps/playground`, `apps/conformance`) needs the same container at its root, a
boot gate. It discovers the surface's runtime configuration, stores it, themes the app from its branding, and shows a
loading or error state until then. That container depends on both `@ncode/store` and `@ncode/ui`, so no shared package
could hold it, and each app carried its own copy. The copies were identical except for a doc comment, and their tests
had already diverged. Each app also carried a `bootstrap.ts` that bound the page origin and its surface name to
`loadRuntimeConfig`, and those modules differed only in that name.

The surface name is a `SurfaceKind`, which was a closed union of the four surfaces in this repository. The host maps
surface names to their coordinates through an open dictionary (`FrontendBootstrapOptions.Surfaces`), so the closed
union was the only thing a new surface would have had to edit a library for.

## Decision

**Stateful containers shared by more than one surface live in `@ncode/shell`, the one package that may depend on both
`@ncode/store` and `@ncode/ui`.** `@ncode/ui` stays stateless and store-free, and `@ncode/store` and `@ncode/config` stay
UI-free. A container that only one surface uses (the Management console's `AuthGate`) stays in its app until a second
surface needs it.

1. **`BootstrapGate` takes its loader as a prop.** `load` is a `RuntimeConfigLoader`
   (`(signal: AbortSignal) => Promise<RuntimeConfig>`, from `@ncode/config`), so a test passes its own loader instead of
   mocking a module. The gate's state rules:
   - The loader is a dependency of the loading effect. A new loader abandons an unfinished load and starts a fresh
     attempt.
   - A failure belongs to the loader that produced it, so a new loader clears it without a state reset.
   - Only the current load may report. A load that ignores its abort signal and settles late is dropped.
   - Configuration that reaches the store any other way wins over a load in flight or a failed one.
2. **The gate's loading and error views are a stateless `@ncode/ui` component, `BootstrapScreen`, with stories.** This
   is the same split as `AuthGate` and `SignInScreen`. Both views render before any branding is known, so they use the
   default theme. The children get the branded theme.
3. **Each surface declares itself once, in its composition root.** `main.tsx` passes
   `createRuntimeConfigLoader({ surface: 'mgmt' })` to the gate. `loadRuntimeConfig` defaults the serving origin to the
   page's own origin (`location.origin`), the way it defaults `fetch` to the global one. The page origin is the serving
   host of every bundle, because a standalone dev server proxies the bootstrap document. No app module binds the origin
   or the surface.
4. **`SurfaceKind` is an open vocabulary** ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). It is
   the shipped surface names plus any string (`… | (string & {})`), so the known names still autocomplete and a new
   surface needs no change to `@ncode/config`.

## Options considered

- **Put the gate in `@ncode/ui`.** Rejected: `@ncode/ui` components are stateless and render without a store in
  Storybook and unit tests (ADR-0064). A store-backed container there breaks that for the whole package.
- **Put the gate in `@ncode/store`.** Rejected: it would make the UI-free state package depend on Material UI.
- **Give the gate a `surface` prop and let it call `loadRuntimeConfig` itself.** Rejected: tests would have to mock a
  module to control the load, and a surface that loads its configuration differently (another origin, a fixture) could
  not use the gate.
- **Keep a `bootstrap.ts` per app as the surface binding.** Rejected: three copies of the same two functions and their
  tests, differing by one string, is the duplication this removes.
- **A `mountSurface` helper that owns `createRoot`, the store `Provider`, and the gate.** Rejected: `main.tsx` stays the
  explicit composition root. Hiding the `Provider` would hide the requirement that the gate sits inside it, to save a
  few lines per app.
- **Keep `SurfaceKind` closed to catch a misspelled surface at compile time.** Rejected: a misspelled surface receives
  the host default, which fails visibly at the first sign-in. A closed union makes every new surface edit a library.

## Consequences

- One gate and one test suite serve every standalone surface, so a fix lands once. The suite covers abandoned and late
  loads, StrictMode remounts, and a loader replaced after a failure.
- A surface's only boot-time binding is the `surface` it passes in `main.tsx`.
- The next container shared by two surfaces has a home with the dependencies it needs.
- Amends [ADR-0064](0064-frontend-presentation-platform.md) (the app topology gains `@ncode/shell`) and
  [ADR-0082](0082-playground-and-conformance-surfaces.md) (each app declares its surface in `main.tsx`, and
  `SurfaceKind` is open).

## References

- [`src/frontend/packages/shell`](../../src/frontend/packages/shell),
  [`packages/ui/src/components/BootstrapScreen.tsx`](../../src/frontend/packages/ui/src/components/BootstrapScreen.tsx),
  [`packages/config/src/loader.ts`](../../src/frontend/packages/config/src/loader.ts), each standalone app's
  `src/main.tsx`
- [ADR-0064](0064-frontend-presentation-platform.md) (platform layering),
  [ADR-0082](0082-playground-and-conformance-surfaces.md) (surface-aware bootstrap),
  [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) (open vocabularies),
  [ADR-0157](0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md) (the shared build preset)
- Backlog `U18`
