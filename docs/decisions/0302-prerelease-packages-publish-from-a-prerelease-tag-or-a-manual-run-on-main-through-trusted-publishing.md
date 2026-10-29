# 302. Prerelease packages publish from a prerelease tag or a manual run on main, through trusted publishing

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

The first sandbox deployment consumes the family's packages from nuget.org, but no workflow published anything
(issue #324). `GOVERNANCE.md` "Publishing" already allowed prereleases to publish unsigned from protected refs, and
reserved signed, timestamped release packages for a release tag (issue #184). Nerdbank.GitVersioning versions every
package in lockstep from `version.json`; on `main` and on a `v*` tag, which `publicReleaseRefSpec` names, the version
has no commit-height suffix, so every commit of `main` packs the same prerelease version (`0.0.1-alpha`) until
`version.json` changes.

[ADR-0143](0143-ci-presents-test-results-and-coverage-in-github.md) keeps the one write-scoped permission CI needs, for
the coverage upload, out of the job that runs third-party build and test code.

## Decision

**A prerelease publishes from a prerelease tag on `main` or a release line, or from a manual run on `main`, in a
workflow whose publishing job holds only a short-lived nuget.org key issued through trusted publishing.**

1. **Deliberate triggers.** `.github/workflows/publish-prerelease.yml` runs on a tag `v*.*.*-*` or a manual run on
   `main`, never on every merge. A new prerelease is a bump of `version.json`'s prerelease version, merged to `main`,
   then tagged `v<version>` or run by hand.
2. **Only protected refs publish.** A tag must name a commit that `main` or a release line already holds, so a tag on an
   unreviewed commit publishes nothing.
3. **One prerelease version, the one the tag names.** After the full Definition of Done packs every shippable project,
   `build/package-version.ps1` requires the packages to share one version, refuses a release version (the scope of
   #184), and requires a tag to name exactly that version.
4. **Two jobs.** `build` runs the gate and uploads the packages with read-only permissions. `publish` runs in the
   protected `nuget` environment, with `id-token: write` only, and exchanges the job's OIDC token for a short-lived API
   key with `NuGet/login`, through the nuget.org trusted publishing policy for this repository, workflow and
   environment. No long-lived API key exists.
5. **A version publishes once.** The publish job refuses a version every package of which is already on nuget.org, so a
   run on an unchanged `version.json` fails instead of reporting success for nothing; it pushes with
   `--skip-duplicate`, so a run that failed part way completes when run again. Each package's `.snupkg` goes to the
   symbol server with it.

## Options considered

- **Publish every merge to `main`.** Rejected: nuget.org versions are permanent, and a version per merge buries the
  ones a deployment needs.
- **A `{height}` suffix in `version.json`.** Rejected for now: a tag could then name a version only after the commit
  exists, and the prerelease name is a decision, not a counter.
- **A long-lived API key in a secret.** Rejected: trusted publishing issues a key that lives for the job, scoped to
  this workflow.

## Consequences

- The maintainers configure a nuget.org trusted publishing policy for `NCodeGroup/Identity`, this workflow and the
  `nuget` environment, the `nuget` environment with required reviewers and deployment rules for `main` and `v*` tags,
  and the repository variable `NUGET_USER`.
- Release publishing (#184) reuses the policy, the environment and the two-job shape, adding signing, timestamping and
  `dotnet nuget verify`.

## References

- [nuget.org trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)
- [GitHub Actions OIDC](https://docs.github.com/actions/security-for-github-actions/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
- [ADR-0143](0143-ci-presents-test-results-and-coverage-in-github.md) — no write-scoped permission in the job that runs
  third-party code
- Issues [#324](https://github.com/NCodeGroup/Identity/issues/324) and
  [#184](https://github.com/NCodeGroup/Identity/issues/184)
