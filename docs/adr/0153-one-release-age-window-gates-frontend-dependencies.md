# 153. One release-age window gates frontend dependencies for people and Dependabot

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A compromised npm release is usually detected and unpublished within hours to days, so waiting before adopting a new
version (a release-age gate, or cooldown) keeps most of them out. Both tools that resolve the frontend workspace's
dependencies can enforce one: pnpm through `minimumReleaseAge`, and Dependabot through `cooldown`.

- **Dependabot applies a cooldown by default.** When `dependabot.yml` configures none, Dependabot waits three days
  before proposing a version.
- **On pnpm 10.16 and later, Dependabot also applies the cooldown to the whole resolution.** It runs
  `pnpm update <package> -r --lockfile-only --no-save --config.minimum-release-age=<cooldown>`, and pnpm then requires
  every version that resolution touches to be at least that old, including versions already in the lockfile. On
  pnpm 10 a younger one fails with `ERR_PNPM_NO_MATURE_MATCHING_VERSION`. Dependabot retries without the gate only on
  pnpm 11's wording of the error, and the setting that skips re-checking locked versions (`trustLockfile`) needs
  pnpm 11.3.
- **People resolve without a gate unless the workspace sets one.** A `pnpm install`, a `pnpm add`, or a hand-edited
  range locks the newest version. A version locked while it is younger than the cooldown fails every Dependabot npm
  version update until it ages.
- **Dependabot runs on its own Node.** Its updater ran Node 24 against the workspace's `engines.node` of `>=26.0.0`, and
  pnpm reported only a warning.

## Decision

One three-day release-age window gates every resolution of the frontend workspace, whether a person or Dependabot
runs it.

1. **pnpm enforces it.** `pnpm-workspace.yaml` sets `minimumReleaseAge: 4320` (minutes).
2. **Dependabot's npm cooldown is stated, and it is never longer than pnpm's window.** `dependabot.yml` sets
   `cooldown: default-days: 3`. With equal windows Dependabot defers to the workspace setting instead of overriding it.
   A longer cooldown would let pnpm lock versions that Dependabot then rejects.
3. **An urgent version is excluded explicitly.** A version needed before it is three days old (a fix that does not
   arrive as a Dependabot security update) is listed in `minimumReleaseAgeExclude` with a comment saying why, and the
   entry is removed once the version ages. Dependabot security updates already bypass both gates.
4. **`engines.node` stays advisory.** The workspace does not set `engineStrict`, because Dependabot's updater runs a
   Node of its own choosing.

## Options considered

- **Turn Dependabot's cooldown off** (`default-days: 0`). Rejected: it removes a supply-chain control to hide a
  mismatch between two settings, when the mismatch is the thing to fix.
- **Rely on Dependabot's default without stating it.** Rejected: if GitHub lengthened the default, the gap between the
  two windows would reopen without any change in this repository.
- **A one-day window for both.** Not chosen: it would shorten Dependabot's default. Updates arrive weekly, so three days
  delays almost nothing.
- **Go back to pnpm 9**, which has no release-age gate, so Dependabot applies none. Rejected: it hides the gate
  instead of honoring it, on an unmaintained pnpm major
  ([ADR-0152](0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md)).
- **Roll back the versions younger than three days now.** Rejected: it would undo deliberate upgrades, and those
  versions age out before Dependabot's next weekly run.

## Consequences

- The lockfile cannot gain a version Dependabot's gate rejects unless someone excludes it deliberately, and then only
  until that version ages.
- `pnpm add <package>` resolves the newest version that is at least three days old. Asking for a newer one fails with
  `ERR_PNPM_NO_MATURE_MATCHING_VERSION`.
- `pnpm install --frozen-lockfile` does not re-check ages, so CI and `build/frontend-install.ps1` install the committed
  lockfile as it is. The versions it held when this was decided that were younger than three days make Dependabot's npm
  version update fail until 2026-10-22, which is before the next weekly run.
- pnpm 11 and later read the same setting from the same file and add `minimumReleaseAgeStrict` and `trustLockfile`;
  issue [#153](https://github.com/NCodeGroup/Identity/issues/153) revisits those.

## References

- [`src/frontend/pnpm-workspace.yaml`](../../src/frontend/pnpm-workspace.yaml),
  [`.github/dependabot.yml`](../../.github/dependabot.yml)
- [ADR-0152](0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md) (pnpm 10 and the other
  supply-chain settings)
- pnpm: [`minimumReleaseAge`](https://pnpm.io/10.x/settings) and `minimumReleaseAgeExclude`
- Dependabot: [`cooldown`](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference)
  ("If not specified, Dependabot applies a default cooldown of 3 days"); dependabot-core
  `npm_and_yarn/lib/dependabot/npm_and_yarn/file_updater/pnpm_lockfile_updater.rb` (the pnpm release-age gate)
