# 1503. Every merge to main publishes the host image, the console image and the chart at one prerelease version

- **Status:** Accepted (refines
  [ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md),
  [ADR-1178](1178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md),
  [ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md) and
  [ADR-1458](1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md))
- **Date:** 2026-11-12
- **Deciders:** NCode Group

## Context

The host image, the console image and the host chart are what a deployment runs
([ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md)). A consumer deploys every merge to `main`
automatically, so each merge needs artifacts to deploy. Nerdbank.GitVersioning gives a public-release build one version
string for every commit, so it cannot tell two merges apart. The chart pins its image tags to its own `appVersion`, so a
consumer follows the chart's version alone.

## Decision

**Every push to `main` and every release tag publishes the host image, the console image and the chart at one version.**

1. **The `Publish artifacts` workflow publishes on every push to `main`** and on a manual run on `main`. Its jobs are
   `version`, `publish` and `verify`. It publishes `ghcr.io/ncodegroup/ncode-identity-host:V`,
   `ghcr.io/ncodegroup/ncode-identity-management-console:V` and
   `oci://ghcr.io/ncodegroup/charts/ncode-identity-host:V`. The images are linux/amd64 and linux/arm64 and carry the
   source, revision, version, licenses and title labels. A release tag `v*.*.*` on `main` or a release line publishes
   the same three artifacts at the tag's version.
2. **The version is `<version.json prerelease>.<first-parent commit count>`**, such as `0.0.1-alpha.57`, printed by
   `build/artifact-version.ps1`. The image tags, the chart `version` and the chart `appVersion` are equal
   (`helm package --version V --app-version V`). A semver range that includes prereleases, such as `>=0.0.1-alpha`,
   selects the newest. A release tag publishes at its own version, which `build/artifact-version.ps1 -Tag` checks
   against the `major.minor` line of `version.json`; a stable version also tags both images `latest`, and a prerelease
   never does.
3. **Each artifact is signed and attested.** `cosign sign` signs each of the three digests keylessly, under the identity
   `https://github.com/NCodeGroup/identity/.github/workflows/publish-artifacts.yml@refs/heads/main`, or
   `@refs/tags/<tag>` for a release tag. `actions/attest-build-provenance` attests each digest. The images carry a BuildKit SBOM.
4. **The `verify` job pulls every artifact anonymously.** It holds `contents: read` only and logs in to no registry, so a
   private or unreachable package fails the run.
5. **The publishing job runs only first-party code on `main`**, reviewed through the merge queue. It is not split from
   the build the way [ADR-1143](1143-ci-presents-test-results-and-coverage-in-github.md) splits the privileged upload
   from third-party build code.
6. **No approval gate guards the publish.** There is no environment approval.
7. **The images report the version they were published at.** Each Dockerfile takes the optional build argument
   `VERSION` and writes it over the version in `version.json` inside the build stage, because the git engine is
   disabled there and `version.json` is the only input; the assemblies' informational version then carries it. A build
   without the argument keeps the version in `version.json`.

## Options considered

- **Publishing from a tag only.** Rejected: a tag is a manual act, and a consumer that deploys every merge needs an
  artifact for every merge.
- **Nerdbank.GitVersioning's version.** Rejected: a public-release build has the same version for every commit, so two
  merges would claim one tag.
- **Image-update automation in the consumer.** Rejected: the chart pins its image tags to its `appVersion`, so following
  the chart follows the images.

## Consequences

- A consumer follows one version range and receives the host, the console and the chart together.
- A consumer that follows every version resets its database when the schema changes. Until the first stable release the
  host's initial migration is edited in place
  ([ADR-1394](1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md)), so the
  pre-deploy migration Job fails and the release stays on the previous version.
- A package created outside this workflow needs this repository's write access under the package's "Manage Actions
  access" and public visibility. Both are GitHub settings that no workflow can change.

## References

- [ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md),
  [ADR-1143](1143-ci-presents-test-results-and-coverage-in-github.md),
  [ADR-1178](1178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md),
  [ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md),
  [ADR-1394](1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md),
  [ADR-1458](1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md)
- `.github/workflows/publish-artifacts.yml`, `build/artifact-version.ps1`