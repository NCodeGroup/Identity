# 152. The frontend's Node and pnpm are each pinned once and provisioned without Corepack

- **Status:** Accepted (amends [ADR-0064](0064-frontend-presentation-platform.md): pnpm is no longer activated with
  Corepack; [ADR-0071](0071-aspire-local-dev-orchestrator.md): the AppHost selects the Node major pinned in
  `.node-version`; [ADR-0147](0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md): the
  runtime is Node 26 and `engines.node` is its floor; amended by
  [ADR-0153](0153-one-release-age-window-gates-frontend-dependencies.md): pnpm also enforces Dependabot's three-day
  release-age window; and by
  [ADR-0249](0249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md): the host image names
  the Node major as a literal that the Host image workflow checks against .node-version)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

The frontend toolchain needs a Node runtime, a pnpm version, and an `@types/node` major that matches the runtime.
Choosing and provisioning them involves these forces, checked upstream on 2026-10-19:

- **Release schedule.** Node 22 is in Maintenance LTS until 2027-04-30. Node 24's Active LTS ends 2026-10-20, and it
  reaches end of life on 2028-04-30. Node 26 (26.12.0) becomes Active LTS on 2026-10-28 and is supported until
  2029-04-30.
- **Node does not ship Corepack.** Node 25 stopped distributing it (nodejs/node#57617), so on Node 26 nothing activates
  the pnpm the `packageManager` field pins unless the project installs it.
- **Dependabot lags pnpm.** pnpm 12.14 is the current release, but GitHub states that Dependabot does not officially
  support pnpm 11 or 12 (dependabot-core#16397), and it reads pnpm 12's multi-document lockfile as an empty dependency
  graph (dependabot-core#15904), which silently stops security updates. Dependabot officially supports pnpm up to 10.
  It also does not support `devEngines` (dependabot-core#13722) and never bumps the `packageManager` field
  (dependabot-core#4830).
- **A version stated in many places drifts.** CI, the Aspire AppHost and the bootstrap script (to select an NVS
  install), `engines.node`, two `@types/node` ranges, and the documentation all need the Node major. When each states
  it separately, a runtime move has to find every one, and each lookup can select an install its own way — sorting
  `22.9.0` above `22.22.2` as text, or taking the first directory listed.
- **pnpm 10 blocks dependency install scripts by default** and only warns about the ones it skipped (here, esbuild's
  `postinstall`).
- **`@types/node` 26 moves `JsonWebKey`** from `node:crypto` into its `webcrypto` namespace, as
  [ADR-0147](0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md) recorded when Dependabot
  first proposed those types.

## Decision

The frontend runs on Node 26 with pnpm 10. Each version is pinned in exactly one place, and pnpm is installed from its
pin by pnpm's own tooling, not by Corepack.

1. **The frontend runs on Node 26.** It is the LTS line with the longest support. The 26.x line that becomes LTS on
   2026-10-28 is the one the toolchain was verified on, so the promotion changes its label, not the runtime.
2. **`.node-version` is the single source of the Node major, and everything that can read a file reads it.** CI uses
   `actions/setup-node`'s `node-version-file`. The AppHost reads the file relative to its own project directory and
   prepends the newest NVS-managed install of that major to its `PATH`, comparing versions numerically.
   `build/frontend-install.ps1` adds that major through NVS when it is missing, and fails if the `node` on `PATH` is
   another major. Documentation refers to the file, not to a number. The two values that cannot read a file move
   with it, in the same change:
   - `engines.node` states the runtime's floor (`>=26.0.0`). It is also what invalidates Turborepo's task cache when
     the runtime changes: Turborepo hashes the root `package.json`, and its documentation names `engines` as the way
     to account for Node versions.
   - The `@types/node` range, in the root and in `apps/conformance-runner`. The workspace override still forces every
     copy to the root's range
     ([ADR-0147](0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md)).
3. **pnpm is provisioned from its own pin, not through Corepack.** The `packageManager` field pins the exact version.
   CI installs it with `pnpm/action-setup`, which also caches the pnpm store; that replaces four hand-written steps.
   The action is pinned to a commit, like every third-party action. Locally, `build/frontend-install.ps1` installs
   that version with the pinned Node's npm into npm's global prefix. Under NVS that prefix is the Node directory the
   AppHost already prepends, so `pnpm` resolves beside `node`. A globally installed pnpm 10 or later switches to the
   pinned version by itself (`manage-package-manager-versions`).
4. **pnpm stays on the newest major Dependabot supports, which is 10** — the same rule
   [ADR-0147](0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md) applies to TypeScript.
   Dependabot does not bump `packageManager`, so the pin moves only by a deliberate change. Issue [#153](https://github.com/NCodeGroup/Identity/issues/153) tracks
   pnpm 12.
5. **Every pnpm setting lives in `pnpm-workspace.yaml`.** That is the only place pnpm 11 and later read settings from,
   so this replaces `.npmrc` and the `package.json` `pnpm` field.
6. **Dependency install scripts are denied unless reviewed.** `strictDepBuilds: true` makes an unreviewed build script
   fail the install instead of printing a warning, and `allowBuilds` records each decision. esbuild is set to `false`:
   its `postinstall` only re-verifies the platform binary that pnpm already installs as an optional dependency.

## Options considered

- **Node 24.** Rejected: its Active LTS ends the day after this decision, and it reaches end of life a year before
  Node 26, so the next move would come a year sooner.
- **Stay on Node 22 until Node 26 is labelled LTS on 2026-10-28.** Rejected: the label changes nine days later on the
  same release line, and the whole toolchain already passes on 26.12.0. Waiting would mean doing this change twice.
- **Install Corepack from npm.** Rejected: it adds back to every machine and runner a shim layer that Node itself
  dropped. pnpm and its official action provision the pinned version directly.
- **Let pnpm 12 manage Node as well** (`devEngines.runtime` with `pnpm/setup`), which would make the runtime a locked,
  checksummed dependency and remove NVS and `setup-node`. Deferred to [#153](https://github.com/NCodeGroup/Identity/issues/153), not rejected: Dependabot supports neither
  pnpm 12 lockfiles nor `devEngines`, so adopting it now would stop security updates.
- **Allow esbuild's build script.** Rejected: nothing needs it, and least privilege says a script runs only when a
  dependency cannot work without it.

## Consequences

- The frontend runtime is supported until 2029-04-30.
- Moving to the next Node major means changing `.node-version`, `engines.node`, the two `@types/node` ranges, the Node
  image of the host Dockerfile, whose major the `Host image` workflow checks
  ([ADR-0249](0249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md)), plus whatever code
  the new types break. CI, the AppHost, the bootstrap script, and the documentation follow the file.
- Each developer re-runs `build/frontend-install.ps1` once. It adds Node 26 through NVS and installs pnpm 10. pnpm 10
  then replaces the `node_modules` that pnpm 9 built, without the interactive confirmation it would otherwise ask
  for. The AppHost's NVS lookup remains Windows-only, as before; any other version manager works when `node` on `PATH`
  is the pinned major.
- `apps/conformance-runner` types its JWKs as `webcrypto.JsonWebKey`, and `KeyObject.export({ format: 'jwk' })` is
  typed, so the cast on it is gone.
- A new dependency with an install script fails `pnpm install` until someone records a decision in `allowBuilds`.
  pnpm keeps an `ignoredBuilds` entry in `node_modules/.modules.yaml` for a package it does not rebuild, so a
  `node_modules` installed before the decision existed keeps failing; delete it and reinstall.

## References

- [`src/frontend/.node-version`](../../src/frontend/.node-version),
  [`src/frontend/package.json`](../../src/frontend/package.json),
  [`src/frontend/pnpm-workspace.yaml`](../../src/frontend/pnpm-workspace.yaml),
  [`.github/workflows/frontend.yml`](../../.github/workflows/frontend.yml),
  [`build/frontend-install.ps1`](../../build/frontend-install.ps1),
  [`src/NCode.Identity.OpenId.LocalDev/Program.cs`](../../src/NCode.Identity.OpenId.LocalDev/Program.cs)
- Node.js: [release schedule](https://github.com/nodejs/Release#release-schedule);
  [`build: stop distributing Corepack`](https://github.com/nodejs/node/pull/57617) (Node 25)
- pnpm: [installation](https://pnpm.io/installation);
  [`strictDepBuilds` and `allowBuilds`](https://pnpm.io/10.x/settings);
  [`pnpm/action-setup`](https://github.com/pnpm/action-setup)
- Dependabot:
  [supported ecosystems](https://docs.github.com/en/code-security/reference/supply-chain-security/supported-ecosystems-and-repositories);
  dependabot-core [#15904](https://github.com/dependabot/dependabot-core/issues/15904),
  [#16397](https://github.com/dependabot/dependabot-core/issues/16397),
  [#13722](https://github.com/dependabot/dependabot-core/issues/13722),
  [#4830](https://github.com/dependabot/dependabot-core/issues/4830)
- Turborepo: handling platforms (Node.js versions through `engines`)
