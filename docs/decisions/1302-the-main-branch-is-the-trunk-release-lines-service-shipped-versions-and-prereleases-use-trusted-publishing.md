# 1302. The main branch is the trunk, release lines service shipped versions, and prereleases use trusted publishing

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0004](project-template/0004-trunk-based-development-on-the-default-branch.md)

## Context

[project-template ADR-0004](project-template/0004-trunk-based-development-on-the-default-branch.md) makes the default
branch the one trunk and leaves release lines to a decision of each project. The family publishes NuGet packages in
lockstep ([ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md));
a consumer pins a `major.minor` and expects patches for it while `main` moves on.

Nerdbank.GitVersioning stamps a clean version, with no commit-height suffix, only on the refs that
`publicReleaseRefSpec` in [`version.json`](../../version.json) matches: `main`, the `release/vX.Y` lines and `v*` tags.
So every commit of `main` packs the same prerelease version until `version.json` changes. The hosted platform deploys
these builds, and a branch records no approval for a deployment.

Two other forces shape the rule. The merge queue already tests every contributor's branch on the merged result, and
release lines already keep shipped versions apart from work in progress. And the one write-scoped permission CI needs,
for the coverage upload, stays out of the job that runs third-party build and test code
([ADR-1143](1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md)).

## Decision

**`main` is the one trunk; each shipped `major.minor` is serviced from a long-lived release line beside it; and a
prerelease publishes from a deliberate trigger through a short-lived nuget.org key.**

### The trunk

1. **`main` is the one trunk at any team size or release state.** There is no long-lived integration branch. Work lands
   through short-lived branches that target `main` and are deleted on merge.
2. **Tooling keys off the trunk.** The host's status and scanning attach to the default branch, and
   `publicReleaseRefSpec` in `version.json` matches `main`, `release/vX.Y` and `v*` tags, so those builds carry a clean
   version.

### Release lines

3. **A release line is `release/vX.Y`** (`version.json` `release.branchName`), cut from `main` with
   `nbgv prepare-release`, which creates the branch and moves `main` to the next prerelease version. The first stable
   release cuts the first line.
4. **A release is an annotated tag `vX.Y.Z` on its release line,** never moved or deleted. A prerelease tag
   `vX.Y.Z-<suffix>` may name a commit on `main`. Only a major goes through a release candidate. `AGENTS.md`'s Landing
   section keeps `release: none`, so a plan never tags.
5. **Two rulesets protect the lines and tags.**
   - The `release-lines` ruleset (`refs/heads/release/v*`, no bypass actors) requires a pull request with every thread
     resolved, the same six required checks with the branch up to date (strict), linear history, no force push and no
     deletion. It has no merge queue, which does not apply to a wildcard branch pattern.
   - The `release-tags` ruleset (`refs/tags/v*`) lets a tag be created but never deleted, updated or force-moved.
6. **Servicing is upstream-first.** A fix merges to `main`, then a backport pull request cherry-picks it with
   `git cherry-pick -x` onto `backport/<pr>-vX.Y`, cut from `release/vX.Y`. A `backport vX.Y` label makes the `Backport`
   workflow (`build/backport.ps1`) do it; the workflow comments when the cherry-pick conflicts or the line does not
   exist. A fix that cannot apply to `main` lands on the line by a pull request that states why. A patch release is a
   `vX.Y.Z` tag on the line, and a hotfix follows the same flow, expedited.
7. **Deployment is gated by an environment, not a branch.** Promotion to a hosted environment goes through a GitHub
   deployment environment with its own required reviewers and secrets, so the approval is recorded against the
   deployment.

### Prerelease publishing

8. **Deliberate triggers.** `.github/workflows/publish-prerelease.yml` runs on a tag `v*.*.*-*` or a manual run on
   `main`, never on every merge. A new prerelease is a bump of `version.json`'s prerelease version merged to `main`,
   then tagged `v<version>` or run by hand.
9. **Only protected refs publish.** A tag must name a commit that `main` or a release line already holds, so a tag on an
   unreviewed commit publishes nothing.
10. **One prerelease version, the one the tag names.** After the full Definition of Done packs every shippable project,
    `build/package-version.ps1` requires the packages to share one prerelease version, refuses a release version
    (signed release publishing is issue [#184](https://github.com/NCodeGroup/identity/issues/184)), and requires a tag
    to name exactly that version.
11. **Two jobs.**
    - `build` runs the gate and uploads the packages with read-only permissions.
    - `publish` runs in the protected `nuget` environment (required reviewers, deployment rules for `main` and `v*`
      tags) with `contents: read` and `id-token: write`. It exchanges its OIDC token for a job-lived API key through
      nuget.org trusted publishing (`NuGet/login`, repository variable `NUGET_USER`), under a policy for this
      repository, workflow and environment. No long-lived API key exists.
12. **A version publishes once.** The publish job refuses a version every package of which is already on nuget.org, so
    a run on an unchanged `version.json` fails instead of reporting success for nothing. It pushes with
    `--skip-duplicate`, so a run that failed part way completes when run again, and pushes each package's `.snupkg` to
    the symbol server.

## Options considered

- **An integration branch.** Rejected: it adds a merge step and splits history for isolation that nothing lacks,
  because release lines keep shipped versions apart and the merge queue tests every branch on the merged result.
- **Releases tagged on `main` only.** Rejected: a consumer of `X.Y` could get a fix only together with everything else
  `main` has gained since.
- **Publishing every merge to `main`.** Rejected: nuget.org versions are permanent, and a version per merge buries the
  ones a deployment needs.
- **A `{height}` suffix in `version.json`.** Rejected: a prerelease name is a decision, not a counter, and a tag could
  then name a version only after the commit exists.
- **A long-lived API key in a secret.** Rejected: trusted publishing issues a key that lives for the job, scoped to this
  workflow.
- **One trunk, release lines beside it, deliberate prerelease publishing through trusted publishing (chosen).**

## Consequences

- `main` is always the newest verified state; a release line takes only the fixes backported to it.
- Each fix a release line needs costs one backport pull request.
- Release publishing (issue #184) reuses the trusted publishing policy, the `nuget` environment and the two-job shape,
  adding signing, timestamping and `dotnet nuget verify`.
- The `nuget` environment, the trusted publishing policy for `NCodeGroup/identity` and the `NUGET_USER` variable are
  configured on the host, not in the repository.

## References

- [project-template ADR-0004](project-template/0004-trunk-based-development-on-the-default-branch.md) — the trunk this
  record refines.
- [project-template ADR-0001](project-template/0001-conventions-and-lessons-live-in-the-repository.md) — why this stance
  is recorded in the repository.
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md) — the
  greenfield pre-release posture this decision shares.
- [ADR-1261](1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md)
  — the pull request rule on `main`.
- [ADR-1263](1263-branches-and-pull-requests-run-an-affected-gate-and-the-merge-queue-runs-the-full-gate.md) — the gates
  of branches and release lines.
- [ADR-1143](1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md)
  — no write-scoped permission in the job that runs third-party code.
- [`GOVERNANCE.md`](../../GOVERNANCE.md#releases-branches--tags), [`CONTRIBUTING.md`](../../CONTRIBUTING.md) and
  [`version.json`](../../version.json).
- [nuget.org trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) and
  [GitHub Actions OIDC](https://docs.github.com/actions/security-for-github-actions/security-hardening-your-deployments/about-security-hardening-with-openid-connect).
- Issues [#324](https://github.com/NCodeGroup/identity/issues/324),
  [#184](https://github.com/NCodeGroup/identity/issues/184) and
  [#439](https://github.com/NCodeGroup/identity/issues/439).
