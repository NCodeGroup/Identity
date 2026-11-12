# 1152. The frontend pins Node and pnpm once, gates dependencies by release age, and tracks supported upstreams

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0009](project-template/0009-new-dependency-versions-wait-a-cooldown.md)

## Context

The frontend toolchain needs a Node runtime, a pnpm version, an `@types/node` major that matches the runtime, and a
policy for how fast new dependency releases enter the lockfile. These forces shape the choices:

- **Release schedule.** Node 26 is the LTS line with the longest support. Node 24 reaches end of life a year before it,
  so a runtime on 24 would force the next move a year sooner.
- **Node does not ship Corepack.** Node 25 stopped distributing it (nodejs/node#57617), so on Node 26 nothing activates
  the pnpm the `packageManager` field pins unless the project installs it.
- **Dependabot lags pnpm.** GitHub states that Dependabot does not officially support pnpm 11 or 12
  (dependabot-core#16397), and it reads pnpm 12's multi-document lockfile as an empty dependency graph
  (dependabot-core#15904), which silently stops security updates. It officially supports pnpm up to 10. It also does not
  support `devEngines` (dependabot-core#13722) and never bumps the `packageManager` field (dependabot-core#4830).
- **A version stated in many places drifts.** CI, the Aspire AppHost and the bootstrap script (to select an NVS
  install), `engines.node`, the `@types/node` ranges, the Dockerfiles and the documentation all need the Node major.
  When each states it separately, a runtime move has to find every one, and each lookup can select an install its own
  way, such as sorting `22.9.0` above `22.22.2` as text.
- **pnpm 10 blocks dependency install scripts by default** and only warns about the ones it skipped (here, esbuild's
  `postinstall`).
- **Two gates meet on one resolution.** On pnpm 10.16 and later Dependabot applies its `cooldown` to the whole
  resolution, locked versions included. A Dependabot cooldown shorter than pnpm's own `minimumReleaseAge` makes pnpm
  reject a version Dependabot just proposed (`ERR_PNPM_NO_MATURE_MATCHING_VERSION`).
- **Coupled majors.** Vite, Vitest and the ESLint plugins move their majors together, and `@types/node` 26 types
  `JsonWebKey` in its `webcrypto` namespace rather than in `node:crypto`.
- **The newest major of a tool is not always a supported one.** typescript-eslint supports TypeScript only up to the 6.x
  line, and Dependabot supports pnpm only up to 10.

## Decision

The frontend runs on Node 26 with pnpm 10. Each version is pinned in exactly one place, pnpm is installed from its pin
by pnpm's own tooling and never by Corepack, one three-day window gates every resolution, and each tool stays on the
newest major its upstream supports.

1. **The frontend runs on Node 26.** `frontend/.node-version` is the single source of the Node major, and everything that
   can read a file reads it:
   - CI uses `actions/setup-node`'s `node-version-file`.
   - The AppHost reads the file relative to its own project directory and prepends the newest NVS install of that major
     to its `PATH`, comparing versions numerically. Its fail-fast when no Node of the pinned major is available is
     [ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md)'s.
   - `build/frontend-install.ps1` adds that major through NVS when it is missing, and fails when the `node` on `PATH` is
     another major.
   - Documentation names the file, not a number.
2. **Values that cannot read the file move with it, in one change.**
   - `engines.node` states the runtime's floor (`>=26.0.0`). It is also what invalidates Turborepo's task cache when the
     runtime changes: Turborepo hashes the root `package.json`, and its documentation names `engines` as the way to
     account for Node versions. It stays advisory, with no `engineStrict`, because Dependabot's updater runs its own
     Node.
   - The `@types/node` range, in the root and in `apps/conformance-runner`.
   - The `node:<major>` image of the OP host and console host Dockerfiles, whose major the `Host image` and
     `Console image` workflows check
     ([ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)).
3. **`@types/node` tracks the runtime major.** The `pnpm-workspace.yaml` override `'@types/node': '$@types/node'` forces
   every transitive and auto-installed copy to the root range, and Dependabot ignores its majors, so a runtime move is
   its own deliberate change.
4. **pnpm is provisioned from its own pin, not through Corepack.** The `packageManager` field pins the exact version.
   CI installs it with `pnpm/action-setup`, pinned to a commit like every third-party action, which also caches the pnpm
   store. Locally, `build/frontend-install.ps1` installs that version with the pinned Node's npm into npm's global
   prefix; under NVS that prefix is the Node directory the AppHost prepends, so `pnpm` resolves beside `node`. A
   globally installed pnpm switches to the pin by itself (`manage-package-manager-versions`).
5. **pnpm stays on 10, the newest major Dependabot supports.** Dependabot never bumps `packageManager`, so the pin moves
   only deliberately. Issue [#153](https://github.com/NCodeGroup/identity/issues/153) tracks pnpm 12 with
   `devEngines.runtime`.
6. **Every pnpm setting lives in `pnpm-workspace.yaml`.** `strictDepBuilds: true` makes an unreviewed install script
   fail the install instead of printing a warning, and `allowBuilds` records each decision. esbuild is `false`: its
   `postinstall` only re-verifies the platform binary that pnpm already installs as an optional dependency.
7. **One three-day window gates every resolution.** It refines
   [project-template ADR-0009](project-template/0009-new-dependency-versions-wait-a-cooldown.md)'s cooldown with pnpm's
   own gate and with how Dependabot applies its cooldown to a pnpm resolution:
   - `pnpm-workspace.yaml` sets `minimumReleaseAge: 4320` (minutes).
   - The npm entry of `.github/dependabot.yml` states `cooldown: default-days: 3`, never longer than pnpm's window.
   - A version needed before it is three days old goes in `minimumReleaseAgeExclude` with a comment, and is removed once
     it ages.
   - Security updates bypass both gates, and `pnpm install --frozen-lockfile` does not re-check ages.
8. **TypeScript stays on the newest major typescript-eslint supports, the 6.x line.** Dependabot ignores TypeScript
   majors (issue [#147](https://github.com/NCodeGroup/identity/issues/147)).
9. **Lint is one flat `eslint.config.js` at the workspace root.** It composes `typescript-eslint` (recommended),
   `@eslint-react/eslint-plugin` (`recommended-typescript`), `eslint-plugin-react-hooks` with `@eslint-react`'s
   `disable-conflict-eslint-plugin-react-hooks` preset, and `eslint-config-prettier` last.
10. **Test type entry points are Vitest-specific** (`@testing-library/jest-dom/vitest`, `vitest/config`), and a workspace
    whose tests import `node:` lists `"node"` in `types`.
11. **The frontend devDependencies update as one Dependabot group** (`frontend-dev-toolchain`), because Vite, Vitest and
    the ESLint plugins are coupled majors.

## Options considered

- **Node 24.** Rejected: it reaches end of life a year before Node 26, so the next runtime move would come a year
  sooner.
- **Install Corepack from npm.** Rejected: it adds back to every machine and runner a shim layer that Node itself
  dropped. pnpm and its official action provision the pinned version directly.
- **Let pnpm manage Node as well** (`devEngines.runtime` with `pnpm/setup`). Rejected for now and deferred to
  [#153](https://github.com/NCodeGroup/identity/issues/153): it would make the runtime a locked, checksummed dependency
  and remove NVS and `setup-node`, but Dependabot supports neither pnpm 12 lockfiles nor `devEngines`, so adopting it
  would stop security updates.
- **Allow esbuild's build script.** Rejected: nothing needs it, and least privilege says a script runs only when a
  dependency cannot work without it.
- **`eslint-plugin-react` behind `fixupPluginRules`.** Rejected: `@eslint-react` is built for flat config and TypeScript,
  and needs no compatibility wrapper.
- **Dropping `eslint-plugin-react-hooks`.** Rejected: the hooks rules stay enforced, with the conflicting `@eslint-react`
  rules disabled by its preset.
- **A separate npm-majors Dependabot group.** Rejected: the coupled majors must move in one pull request, which one
  devDependencies group does.
- **A one-day release-age window.** Rejected: it is shorter than the template's cooldown, which ADR-0009 forbids.
- **pnpm 9 without a gate.** Rejected: it has no `minimumReleaseAge`, so the lockfile would admit a release the day it
  appears.
- **Chosen:** a single `.node-version` read by every consumer that can read a file, pnpm provisioned from its pin, one
  three-day window in both tools, and each tool on the newest major its upstream supports.

## Consequences

- The runtime major is stated once. Moving to the next Node major means changing `.node-version`, `engines.node`, the two
  `@types/node` ranges and the Dockerfiles' Node image, plus whatever code the new types break; CI, the AppHost, the
  bootstrap script and the documentation follow the file.
- `apps/conformance-runner` types its JWKs as `webcrypto.JsonWebKey`, and `KeyObject.export({ format: 'jwk' })` is typed.
- The AppHost's NVS lookup is Windows-only; any other version manager works when `node` on `PATH` is the pinned major.
- A new dependency with an install script fails `pnpm install` until someone records a decision in `allowBuilds`. pnpm
  keeps an `ignoredBuilds` entry in `node_modules/.modules.yaml` for a package it does not rebuild, so a `node_modules`
  installed before the decision existed keeps failing until it is deleted and reinstalled.
- A release younger than three days cannot enter the lockfile except through a commented `minimumReleaseAgeExclude`
  entry, and the exception list stays short because each entry is removed once it ages.
- Dependabot's cooldown and pnpm's window agree, so a pull request Dependabot opens installs without
  `ERR_PNPM_NO_MATURE_MATCHING_VERSION`.
- TypeScript 7's faster type checking is not used until typescript-eslint supports it.
- Security updates are never delayed by the window.

## References

- [`frontend/.node-version`](../../frontend/.node-version),
  [`frontend/package.json`](../../frontend/package.json),
  [`frontend/pnpm-workspace.yaml`](../../frontend/pnpm-workspace.yaml),
  [`frontend/eslint.config.js`](../../frontend/eslint.config.js),
  [`.github/dependabot.yml`](../../.github/dependabot.yml),
  [`.github/workflows/frontend.yml`](../../.github/workflows/frontend.yml),
  [`build/frontend-install.ps1`](../../build/frontend-install.ps1),
  [`hosts/NCode.Identity.OpenId.LocalDev/Program.cs`](../../hosts/NCode.Identity.OpenId.LocalDev/Program.cs)
- Issues [#147](https://github.com/NCodeGroup/identity/issues/147) and
  [#153](https://github.com/NCodeGroup/identity/issues/153)
- [ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md) (the
  frontend platform), [ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md),
  [ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md),
  [project-template ADR-0009](project-template/0009-new-dependency-versions-wait-a-cooldown.md)
- Node.js: [release schedule](https://github.com/nodejs/Release#release-schedule);
  [`build: stop distributing Corepack`](https://github.com/nodejs/node/pull/57617)
- pnpm: [installation](https://pnpm.io/installation);
  [`strictDepBuilds`, `allowBuilds` and `minimumReleaseAge`](https://pnpm.io/10.x/settings);
  [`overrides`](https://pnpm.io/package_json#pnpmoverrides); [`pnpm/action-setup`](https://github.com/pnpm/action-setup)
- Dependabot:
  [supported ecosystems](https://docs.github.com/en/code-security/reference/supply-chain-security/supported-ecosystems-and-repositories);
  [`cooldown`](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference);
  dependabot-core [#15904](https://github.com/dependabot/dependabot-core/issues/15904),
  [#16397](https://github.com/dependabot/dependabot-core/issues/16397),
  [#13722](https://github.com/dependabot/dependabot-core/issues/13722),
  [#4830](https://github.com/dependabot/dependabot-core/issues/4830)
- ESLint: [configuration migration guide](https://eslint.org/docs/latest/use/configure/migration-guide);
  [`@eslint-react`](https://eslint-react.xyz); typescript-eslint
  [supported TypeScript versions](https://typescript-eslint.io/users/dependency-versions)
- Turborepo: handling platforms (Node.js versions through `engines`)
