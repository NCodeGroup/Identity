# 1178. The host image is chiseled, non-root, multi-arch, and carries the login bundle

- **Status:** Accepted (amended by
  [ADR-1248](1248-the-host-image-writes-one-line-json-logs.md): the image writes one-line JSON logs; and by
  [ADR-1249](1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md): its base images are
  pinned by digest, and the Node major is a literal the workflow checks; and by
  [ADR-1313](1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md): a
  Production deployment sets no `DeveloperKeys` section, which replaces `DeveloperKeys:Mode=None`)
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

`NCode.Identity.OpenId.Host` is the deployable ([ADR-1130](1130-distinct-names-for-host-local-dev-orchestrator-and-library.md)),
and its image is the artifact both the hosted platform and self-hosters run
([ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md)). The Dockerfile had been written for Visual
Studio's container tooling. It sent the whole repository to the build, exposed ports 80 and 443 although .NET listens
on 8080, ran as root, and built only for amd64. It also never built the `apps/auth` interactive bundle. Only
`launchSettings.json` set `InteractiveAuthentication:BundleRootPath`, so the container could not render login or
consent. The lab cluster runs on Ampere A1 nodes, so the image must also run on arm64
([hosting analysis](../hosting-analysis.md)).

## Decision

1. **The runtime is the chiseled ASP.NET Core image.** The final stage is `aspnet:10.0-noble-chiseled-extra`. Chiseled
   images are distroless, with no shell and no package manager. The `extra` variant adds ICU and time zone data, so
   culture-sensitive formatting and time zone conversion behave as on a full image. The image runs as the non-root
   `app` user by numeric id (`USER $APP_UID`), so Kubernetes can enforce `runAsNonRoot`. It listens on 8080 over plain
   HTTP and TLS terminates at the ingress.
2. **The image carries the interactive bundle.** A Node stage builds `apps/auth` with the workspace's pinned pnpm. The
   final stage copies its `dist` to `/app/auth` and sets `InteractiveAuthentication__BundleRootPath` to that rooted
   path. The image also carries the migration worker in `/app/migrations`, which the chart runs before each rollout
   ([ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)).
3. **Multi-arch builds without emulation.** The Node and SDK stages run `FROM --platform=$BUILDPLATFORM`. The SDK stage
   publishes framework-dependent for `$TARGETARCH` with no app host. The only stage built per target architecture
   copies files and runs nothing, so `linux/amd64` and `linux/arm64` both build on an amd64 runner without QEMU.
4. **The build context is an allow-list.** `.dockerignore` excludes everything, then re-admits the root build files,
   `src/`, `hosts/`, `analyzers/` and `frontend/`, minus build outputs, `node_modules`, and local settings. Tests,
   docs, `.git`, and secrets never reach the builder.
5. **The Node major follows its single pin.** The Node stage names its image by a literal major, tag and digest
   (`node:<major>-trixie-slim@sha256:…`), because a `FROM` line cannot read a file and Dependabot cannot update a
   `FROM` built from an `ARG`. The `Host image` workflow fails unless that major equals `frontend/.node-version`
   ([ADR-1152](1152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md),
   [ADR-1249](1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md)). pnpm is
   installed from `package.json`'s `packageManager` field.
6. **The version comes from `version.json` alone.** The build context has no `.git`, so the build sets
   `NBGV_GitEngine=Disabled`. Publishing is a release concern: the release workflow tags the image with the version it
   computes from the repository (issue [#87](https://github.com/NCodeGroup/Identity/issues/87)).
7. **CI builds both architectures on every change.** The `Host image` workflow builds the image for `linux/amd64` and
   `linux/arm64` without pushing it.
8. **Visual Studio keeps a debuggable stage.** Container fast mode debugs against the `base` stage, which needs a
   shell, so `base` stays on the full `aspnet` image. The production image never builds from it.

## Consequences

- The image refuses to start under its default `Production` environment until the deployment supplies production
  settings: `AllowedHosts`, `DeveloperKeys:Mode=None`, a protected key ring
  ([ADR-1177](1177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md)), a database, and a tenant
  issuer.
- No shell means no `docker exec` debugging and no Dockerfile `HEALTHCHECK` that runs `curl`. Kubernetes probes
  `/health/live` and `/health/ready` over HTTP instead.
- An image build takes about two minutes on a standard runner. The Node and SDK stages are shared by both
  architectures, and the GitHub Actions cache keeps rebuilds of unchanged layers short.
- The assembly version inside the image omits git height and commit id. The image tag carries the release version.
