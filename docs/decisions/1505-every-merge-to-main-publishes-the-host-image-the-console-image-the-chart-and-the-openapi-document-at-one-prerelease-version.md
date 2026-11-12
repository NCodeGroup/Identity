# 1505. Every merge to main publishes the host image, the console image, the chart and the OpenAPI document at one prerelease version

- **Status:** Accepted
- **Date:** 2026-11-12
- **Deciders:** NCode Group

## Context

The host image, the console image and the host chart are what a deployment runs
([ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md)). A consumer deploys
every merge to `main` automatically, so each merge needs artifacts to deploy. Nerdbank.GitVersioning gives a
public-release build one version string for every commit, so it cannot tell two merges apart. The chart pins its image
tags to its own `appVersion`, so a consumer follows the chart's version alone. Client generators and operators read the
management API description without a deployment serving it, so the OpenAPI document is an artifact of its own at the
same version as the images and the chart.

## Decision

**Every push to `main` and every release tag publishes the host image, the console image, the chart and the OpenAPI
document at one version.**

1. **The `Publish artifacts` workflow publishes on every push to `main`** and on a manual run on `main`. Its jobs are
   `version`, `publish` and `verify`. It publishes `ghcr.io/ncodegroup/ncode-identity-host:V`,
   `ghcr.io/ncodegroup/ncode-identity-management-console:V`,
   `oci://ghcr.io/ncodegroup/charts/ncode-identity-host:V` and
   `oci://ghcr.io/ncodegroup/openapi/ncode-identity-host:V`. The images are linux/amd64 and linux/arm64 and carry the
   source, revision, version, licenses and title labels. A release tag `v*.*.*` on `main` or a release line publishes
   the same four artifacts at the tag's version.
2. **The version is `<version.json prerelease>.<first-parent commit count>`**, such as `0.0.1-alpha.57`, printed by
   `build/artifact-version.ps1`. The image tags, the chart `version` and the chart `appVersion` are equal
   (`helm package --version V --app-version V`). A semver range that includes prereleases, such as `>=0.0.1-alpha`,
   selects the newest. A release tag publishes at its own version, which `build/artifact-version.ps1 -Tag` checks
   against the `major.minor` line of `version.json`; a stable version also tags both images `latest`, and a prerelease
   never does.
3. **Each artifact is signed and attested.** `cosign sign` signs each of the four digests keylessly, under the identity
   `https://github.com/NCodeGroup/identity/.github/workflows/publish-artifacts.yml@refs/heads/main`, or
   `@refs/tags/<tag>` for a release tag. `actions/attest-build-provenance` attests each digest. The images carry a
   BuildKit SBOM.
4. **The `verify` job pulls every artifact anonymously.** It holds read permissions only (`contents` and
   `attestations`) and logs in to no registry, so a private or unreachable package fails the run. It checks both
   architectures of each image, every signature, every provenance attestation, the chart's `version` and `appVersion`,
   and the OpenAPI document's `info.version`.
5. **The publishing job runs only first-party code on `main`**, reviewed through the merge queue. It is not split from
   the build the way
   [ADR-1143](1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md)
   splits the privileged upload from third-party build code.
6. **No approval gate guards the publish.** There is no environment approval.
7. **The images report the version they were published at.** Each Dockerfile takes the optional build argument
   `VERSION`, which builds no `FROM`
   ([ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)),
   and runs `build/stamp-version.ps1` to write it over the version in `version.json` inside the build stage, because
   the git engine is disabled there and `version.json` is the only input; the assemblies' informational version then
   carries it. The script refuses a version with a character outside `[0-9A-Za-z.-]` and fails unless `version.json`
   then holds the version, so an image reports the version its tag names. A build without the argument keeps the
   version in `version.json`.
8. **The OpenAPI document is generated at build time, whatever a deployment serves.** `build/openapi.ps1` builds the
   host and runs the `GenerateOpenApiDocuments` target of `Microsoft.Extensions.ApiDescription.Server` to write
   `artifacts/openapi/v1.json`, in the `Staging` environment with an ephemeral root key and a static issuer, so the host
   starts without a database or a secret store. With `-Version` the script stamps the artifact version as the
   document's `info.version`. The full Definition of Done generates the document, and CI uploads it as the `openapi`
   workflow artifact. The workflow publishes it at every version whatever the `ApiReference` switches of
   [ADR-1481](1481-the-composition-layer-serves-the-api-reference-only-where-a-host-enables-it-and-each-management-problem-says-when-it-occurs.md)
   say, because the document describes the product, not a deployment.

## Options considered

- **Publishing from a tag only.** Rejected: a tag is a manual act, and a consumer that deploys every merge needs an
  artifact for every merge.
- **Nerdbank.GitVersioning's version.** Rejected: a public-release build has the same version for every commit, so two
  merges would claim one tag.
- **Image-update automation in the consumer.** Rejected: the chart pins its image tags to its `appVersion`, so following
  the chart follows the images.
- **Fetching the document from a running host.** Rejected: a running host needs a database and a deployment, and the
  document describes the product, not a deployment.
- **A GitHub release asset.** Rejected: merges to `main` make no release, and the OCI artifact shares the images'
  signing, provenance and version.

## Consequences

- A consumer follows one version range and receives the host, the console, the chart and the OpenAPI document together.
- The pre-deploy migration Job refuses a database whose migration history is ahead of the image's migrations
  ([ADR-1180](1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md)).
- A package created outside this workflow, including `openapi/ncode-identity-host`, needs this repository's write
  access under the package's "Manage Actions access" and public visibility. Both are GitHub settings that no workflow
  can change.

## References

- [ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md),
  [ADR-1143](1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md),
  [ADR-1180](1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md),
  [ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md),
  [ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md),
  [ADR-1481](1481-the-composition-layer-serves-the-api-reference-only-where-a-host-enables-it-and-each-management-problem-says-when-it-occurs.md)
- `.github/workflows/publish-artifacts.yml`, `build/artifact-version.ps1`, `build/stamp-version.ps1`,
  `build/openapi.ps1`
