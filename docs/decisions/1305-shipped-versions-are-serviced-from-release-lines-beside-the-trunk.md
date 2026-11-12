# 1305. Shipped versions are serviced from release lines beside the trunk

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0004](project-template/0004-trunk-based-development-on-the-default-branch.md)
- **Supersedes:** [ADR-1125](1125-trunk-based-development-on-main.md)

## Context

[project-template ADR-0004](project-template/0004-trunk-based-development-on-the-default-branch.md) makes `main` the one
trunk, with no long-lived integration branch, and leaves release lines to a decision of their own. The family publishes
NuGet packages in lockstep ([ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md)); a consumer pins a
`major.minor` and expects patches for it while `main` moves on. Nerdbank.GitVersioning stamps a clean version, with no
commit-height suffix, only on the refs that `publicReleaseRefSpec` in [`version.json`](../../version.json) matches:
`main`, the `release/vX.Y` lines and `v*` tags. The hosted platform deploys these builds, and a branch records no
approval for a deployment.

## Decision

Each shipped `major.minor` is serviced from a long-lived release line beside the trunk; no integration branch is added.

1. **A release line is `release/vX.Y`**, cut from `main` with `nbgv prepare-release`, which creates the branch and moves
   `main` to the next prerelease version. Its builds carry a clean version.
2. **A release is an annotated tag `vX.Y.Z` on its release line.** A prerelease tag `vX.Y.Z-<suffix>` may also name a
   commit on `main`
   ([ADR-1302](1302-prerelease-packages-publish-from-a-prerelease-tag-or-a-manual-run-on-main-through-trusted-publishing.md)).
   A tag is never moved or deleted.
3. **Servicing is upstream-first.** A fix merges to `main` first and is cherry-picked onto the line, as
   [ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) sets out with the
   rulesets that protect the lines and tags.
4. **Neither a second contributor nor the first stable release brings back an integration branch.** A second
   maintainer adds an approval requirement to the pull request rule
   ([ADR-1261](1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md)); the first stable release
   cuts the first release line.
5. **Deployment is gated by an environment, not a branch.** Promotion to a hosted environment goes through a GitHub
   deployment environment with its own required reviewers and secrets, so the approval is recorded against the
   deployment.

## Options considered

- **An integration branch at the first stable release or with a second contributor.** Rejected: release lines already
  keep shipped versions apart from work in progress, and every contributor's short-lived branch is tested by the merge
  queue on the merged result; an integration branch adds a merge step and splits history for isolation nothing lacks.
- **Releases tagged on `main` only.** Rejected: a consumer of `X.Y` could get a fix only together with everything else
  `main` has gained since.
- **Release lines beside the trunk (chosen).**

## Consequences

- `main` is always the newest verified state; a release line takes only the fixes backported to it.
- `AGENTS.md`'s Landing section keeps `release: none`: a plan never tags, and releases are cut on release lines
  ([`GOVERNANCE.md`](../../GOVERNANCE.md#releases-branches--tags)).
- Each fix a release line needs costs one backport pull request.

## References

- [project-template ADR-0004](project-template/0004-trunk-based-development-on-the-default-branch.md) — the trunk this
  record refines.
- [ADR-1125](1125-trunk-based-development-on-main.md) — the record this supersedes; its unprotected trunk was already
  superseded by [ADR-1261](1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md).
- [ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) — the gates, rulesets
  and backports of the release lines.
- [`GOVERNANCE.md`](../../GOVERNANCE.md#releases-branches--tags) and [`version.json`](../../version.json).
- [#439](https://github.com/NCodeGroup/identity/issues/439).
