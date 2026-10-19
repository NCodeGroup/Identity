# 147. The frontend toolchain follows the pinned runtime and supported upstreams

- **Status:** Accepted (amended by
  [ADR-0152](0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md): the runtime is Node 26,
  CI reads `.node-version` itself, `engines.node` states the runtime's floor, and the `@types/node` override lives in
  `pnpm-workspace.yaml`)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

Dependabot updates the frontend `devDependencies` as one group (`frontend-dev-toolchain`). Its first large update moved
eighteen packages at once, eight of them across a major version. The tests passed, but lint, typecheck, and build failed,
and the causes were not in the code:

- **ESLint 9 dropped `.eslintrc.*`.** ESLint 10 reads only a flat `eslint.config.*`, so linting stopped before checking
  a file.
- **`eslint-plugin-react` does not support ESLint 10.** Its last release (7.37.5, April 2025) caps its peer range at
  ESLint 9 and crashes on the context methods ESLint 10 removed (`contextOrFilename.getFilename is not a function`).
  The fix (jsx-eslint/eslint-plugin-react#4022) is unreleased.
- **TypeScript 7 is the native compiler and no longer ships the JavaScript compiler API.** typescript-eslint parses with
  that API and caps its peer range at `<6.1.0`.
- **`@types/node` 26 described a runtime the project does not run.** `.node-version` and CI pin Node 22, and the new
  types removed `JsonWebKey` from `node:crypto`. Even after the root pin was corrected, pnpm's `auto-install-peers`
  kept resolving the `@types/node` peer of Vite and Vitest to 26 in every workspace that does not declare it, so two
  versions of the Node types coexisted.
- **Newer Vitest stopped leaking types.** Vitest 2's declarations pulled `@types/node` and Jest's global matcher
  namespace into every test program. Vitest 5 does neither, so `@testing-library/jest-dom`'s bare entry (which
  augments Jest) and an app's implicit use of Node globals both stopped compiling. Its `test` config key is now
  typed only through `vitest/config`.

None of this is visible in a minor or patch update. It recurs whenever an upstream major changes a contract, so the
rules need to live in the repository rather than be rediscovered per update.

## Decision

1. **`@types/node` tracks the runtime major in `.node-version`.** The root `devDependency` is the single source of the
   range. A `pnpm.overrides` entry (`"@types/node": "$@types/node"`) forces every transitive and auto-installed copy
   to it, and Dependabot ignores its major updates. Moving to a new Node major updates `.node-version`, the CI
   `node-version`, `engines.node`, and this range together (backlog `U16`).
2. **TypeScript stays on the newest version typescript-eslint supports.** Dependabot ignores TypeScript majors, and the
   6.x line is held until typescript-eslint supports TypeScript 7 (backlog `U15`).
3. **Lint uses one flat `eslint.config.js` at the workspace root**, built from maintained plugins:
   - `typescript-eslint` for TypeScript rules (`recommended`, no type information).
   - `@eslint-react/eslint-plugin` (`recommended-typescript`) replaces `eslint-plugin-react`. It is flat-config
     native, supports ESLint 10, and covers the JSX, DOM, and component rules `eslint-plugin-react` provided.
   - `eslint-plugin-react-hooks` (the React team's plugin) adds the React Compiler diagnostics. Its rules that
     duplicate `@eslint-react`'s are turned off by `@eslint-react`'s own `disable-conflict-eslint-plugin-react-hooks`
     preset, so each finding is reported once without a hand-maintained rule list.
   - `eslint-config-prettier` stays last.
4. **Test type entry points are Vitest-specific.** A tsconfig names `@testing-library/jest-dom/vitest`, a Vite config
   references `vitest/config`, and a jsdom app whose tests import `node:` modules lists `"node"` in `types`
   explicitly.
5. **`engines.node` states the real floor** of the toolchain (`>=22.13.0`, set by ESLint 10 and Vitest 5).

## Options considered

- **Keep `eslint-plugin-react` behind `@eslint/compat`'s `fixupPluginRules`.** Rejected: that shims an unmaintained
  plugin past a peer range it does not declare. The repository prefers a supported design over a compatibility
  layer ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- **Use `@eslint-react` alone and drop `eslint-plugin-react-hooks`.** Rejected: the React team's plugin is the reference
  for the Rules of Hooks and the only source of the React Compiler diagnostics (`refs`, `immutability`, `globals`,
  `preserve-manual-memoization`, and others).
- **Move the runtime to Node 26 to match the types.** Deferred: Node 26 enters Active LTS on 2026-10-28, and changing the
  runtime is its own deliberate change (backlog `U16`), not a side effect of a types bump.
- **Split npm majors into their own Dependabot group, as NuGet does.** Not adopted: the frontend majors are coupled
  (Vite, Vitest, and `@vitejs/plugin-react`; ESLint and its plugins) and have to be upgraded together anyway. The
  ignore rules stop the two known-incompatible majors from failing the group every week.

## Consequences

- The grouped toolchain PR can be merged when CI is green, without someone having to re-derive these constraints each time.
- `pnpm audit` is clean. The Vitest/Vite/esbuild advisories in backlog `U14` are gone.
- The stricter React rules surfaced real findings that are now fixed. Two presentational tables read the clock during
  render; they now receive `now` from their container, which reads it once in a state initializer. The conformance
  dashboard derives `isLoading` instead of setting it synchronously in an effect. Refs and state setters follow the
  `…Ref` / `setIs…` naming conventions.
- TypeScript 7's faster type checking is not used until `U15` is resolved.
- Storybook 10 folds the former `addon-essentials` into core, so that addon and the direct `@storybook/react` dependency
  are gone. Stories import their types from the framework package (`@storybook/react-vite`).

## References

- [`src/frontend/eslint.config.js`](../../src/frontend/eslint.config.js),
  [`src/frontend/package.json`](../../src/frontend/package.json), [`.github/dependabot.yml`](../../.github/dependabot.yml)
- [ADR-0064](0064-frontend-presentation-platform.md) (frontend platform),
  [ADR-0143](0143-ci-presents-test-results-and-coverage-in-github.md) (frontend CI reports)
- ESLint: [configuration migration guide](https://eslint.org/docs/latest/use/configure/migration-guide);
  [`@eslint-react`](https://eslint-react.xyz); typescript-eslint
  [supported TypeScript versions](https://typescript-eslint.io/users/dependency-versions)
- pnpm: [`overrides`](https://pnpm.io/package_json#pnpmoverrides) (`$` references to a direct dependency)
- Node.js: [release schedule](https://github.com/nodejs/Release#release-schedule)
