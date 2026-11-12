# 1248. The host and console images are chiseled, non-root, multi-arch, digest-pinned and log one-line JSON

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md), [project-template ADR-0115](project-template/0115-a-host-logs-to-standard-output.md)

## Context

The reference host and the management console's host are deployables, and each ships as a container image that runs in
Kubernetes. An image that serves a production workload has to be small, hold nothing an attacker could use after a
compromise, run without privilege, build for the architectures clusters use, and carry the web bundle it serves, so the
deployment needs no second artifact.

Three more forces shape the images:

- A build must be reproducible. A base image referenced by tag alone moves under the build, and a digest alone gets no
  operating-system fixes unless something moves it on purpose.
- A log aggregator parses what the container writes to standard output. A stack trace that spans lines becomes many
  entries unless the image writes one object per line.
- The version of the Node toolchain has one source, `frontend/.node-version`, and the Dockerfile must agree with it.

## Decision

The host image and the console image are chiseled, non-root, multi-arch and digest-pinned, and they write one-line JSON
logs.

### The runtime image

- The final stage of `hosts/NCode.Identity.OpenId.Host/Dockerfile` is `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`:
  distroless, with no shell or package manager, and with ICU and time-zone data. It runs as the non-root `app` user by
  numeric id (`USER $APP_UID`) and serves HTTPS only, on port 8443, so TLS ends at the pod
  ([ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md) holds the transport rule and the certificate mount).
- The host image builds `apps/auth` in a Node stage with the workspace's pinned pnpm, serves it from `/app/auth` with
  `InteractiveAuthentication__BundleRootPath=/app/auth`, and carries the migration worker in `/app/migrations`.
- `hosts/NCode.Identity.ManagementConsole.Host/Dockerfile` mirrors it with the same pinned bases. It builds
  `@ncode/app-mgmt`, serves `/app/mgmt` with `ManagementConsole__BundleRootPath=/app/mgmt`, and runs as UID 1654 on
  8443.

### Multi-arch without emulation

The Node and SDK stages run `FROM --platform=$BUILDPLATFORM` and publish framework-dependent for `$TARGETARCH` with no
app host, so `linux/amd64` and `linux/arm64` both build on an amd64 runner.

### The build context

`.dockerignore` is an allow-list. It excludes everything, then re-admits the root build files, `src/`, `hosts/`,
`analyzers/` and `frontend/`, `build/stamp-version.ps1`, minus outputs, `node_modules`, `launchSettings.json` and local settings. A new root-imported
build file must be re-admitted there, or only the image checks find it.

### The version

The build stage disables the git engine (`NBGV_GitEngine=Disabled`, no `.git` in the context), so the version comes
from `version.json`, whose assembly version omits git height. The version an image reports is stamped by the optional
`VERSION` build argument, which the Publish artifacts workflow sets on every merge to `main` and every release tag
([ADR-1505](1505-every-merge-to-main-publishes-the-host-image-the-console-image-and-the-chart-at-one-prerelease-version.md)).

### Pinned base images

- Each `FROM` names the tag plus the digest of the multi-architecture index: `node:26-trixie-slim`,
  `mcr.microsoft.com/dotnet/sdk:10.0-noble`, `mcr.microsoft.com/dotnet/aspnet:10.0-noble` for the `base` stage, and
  `aspnet:10.0-noble-chiseled-extra` for the final stage. The `base` stage stays on the full image because Visual
  Studio's fast-mode debugging needs a shell; the final image never builds from it.
- No `ARG` builds a `FROM`: Dependabot cannot update one, and the digest overrides the version it names. Each
  Dockerfile takes one build argument, the optional `VERSION`, which builds no `FROM`. The
  `# syntax=docker/dockerfile:1` frontend stays unpinned.
- The Node major is a literal. The `Host image` and `Console image` workflows fail unless the Dockerfile names
  `node:<major>-trixie-slim@sha256:` for the major in `frontend/.node-version`. The .NET major moves with the target
  framework.
- Dependabot's weekly `docker` entry covers both Dockerfile directories with at most 5 pull requests, a 3-day cooldown,
  the groups `docker-base-images` and `docker-security`, and `version-update:semver-major` ignored. It runs while issue
  #36 pauses the other ecosystems, because a digest gets operating-system fixes only through it.

### Logs

- The final stage of both images sets `Logging__Console__FormatterName=json`,
  `Logging__Console__FormatterOptions__UseUtcTimestamp=true` and `Logging__Console__FormatterOptions__TimestampFormat=O`.
  Each entry is one JSON object on one line, holding the timestamp (UTC, `+00:00`), level, category, event id, message,
  state and an escaped exception.
- The migration worker logs the same way from `/app/migrations`. The `base` stage, the AppHost and
  `dotnet run` keep `simple`. An operator overrides the image with `Logging__Console__FormatterName=simple` through the
  chart's `extraEnv`, and the chart sets no logging value.

### The checks

The `Host image` workflow (a required check) and the `Console image` workflow (job `Build console image`) build both
architectures without pushing, on the same triggers, `merge_group` included. They fail unless `docker image inspect` of
the amd64 image lists the three logging settings and the TLS contract of
[ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md), and exposes only
8443. `ProgramTests` pins the `json` formatter.

## Options considered

- **A `NODE_VERSION` argument beside a digest.** Rejected: the digest overrides the version the argument names, and
  Dependabot cannot update a `FROM` built from an argument.
- **Setting the log format in the chart.** Rejected: the image run another way would write a different format. The
  setting belongs to the image, and the chart only lets an operator override it.
- **Chiseled, non-root, multi-arch and digest-pinned, with the format set in the image (chosen).** The image is small,
  holds no shell, builds reproducibly for both architectures, and writes the same logs wherever it runs.

## Consequences

- The image has no shell, so there is no `docker exec` debugging and no `HEALTHCHECK` that runs `curl`. Kubernetes
  probes `/health/live` and `/health/ready` over HTTPS.
- Under its default `Production` environment the image refuses to start until the deployment supplies production
  settings.
- The base layers of a build are reproducible. A base image moves in a pull request that Dependabot opens and the
  image workflow builds, so the change is reviewed.
- A Node major bump edits `.node-version` and the Dockerfile together. The workflow fails when they differ, and
  Dependabot ignores a major.
- The `FROM` lines are longer than 120 columns, because a digest cannot wrap.
- The output of `docker logs` is JSON, which is harder to read by eye. An operator who reads it directly sets
  `Logging__Console__FormatterName=simple`.
- The three logging settings are a contract of the image. A change that drops one fails the image workflows, and
  `ProgramTests` pins that the formatter they select writes an entry with a multi-line exception as one line that parses
  as JSON.

## References

- [ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md): the shipped artifact
  is the one that is run.
- [ADR-1152](1152-the-frontend-pins-node-and-pnpm-once-gates-dependencies-by-release-age-and-tracks-supported-upstreams.md): the Node major has one
  source.
- [ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md): the
  protected key ring that production settings require.
- [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md): external inputs are
  pinned and kept current.
- [project-template ADR-0115](project-template/0115-a-host-logs-to-standard-output.md): logs go to standard output, and
  their format is configuration.
- Code: `hosts/NCode.Identity.OpenId.Host/Dockerfile`, `hosts/NCode.Identity.ManagementConsole.Host/Dockerfile`,
  `.dockerignore`, `.github/dependabot.yml`, `.github/workflows/host-image.yml`,
  `tests/NCode.Identity.OpenId.Integration.Tests/ProgramTests.cs`.
