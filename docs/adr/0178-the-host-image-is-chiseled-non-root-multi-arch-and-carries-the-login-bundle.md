# 178. The host image is chiseled, non-root, multi-arch, and carries the login bundle

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

`NCode.Identity.OpenId.Host` is the deployable ([ADR-0130](0130-distinct-names-for-host-local-dev-orchestrator-and-library.md)),
and its image is the artifact both the hosted platform and self-hosters run
([ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md)). The Dockerfile had been written for Visual
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
   path.
3. **Multi-arch builds without emulation.** The Node and SDK stages run `FROM --platform=$BUILDPLATFORM`. The SDK stage
   publishes framework-dependent for `$TARGETARCH` with no app host. The only stage built per target architecture
   copies files and runs nothing, so `linux/amd64` and `linux/arm64` both build on an amd64 runner without QEMU.
4. **The build context is an allow-list.** `.dockerignore` excludes everything, then re-admits the root build files
   and `src/`, minus build outputs, `node_modules`, and local settings. Tests, docs, `.git`, and secrets never reach
   the builder.
5. **The Node major comes from its single pin.** A `FROM` line cannot read a file, so the Dockerfile takes
   `NODE_VERSION` as a required build argument. Callers read it from `src/frontend/.node-version`
   ([ADR-0152](0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md)), and the Dockerfile
   carries no default that could drift. pnpm is installed from `package.json`'s `packageManager` field.
6. **The version comes from `version.json` alone.** The build context has no `.git`, so the build sets
   `NBGV_GitEngine=Disabled`. Publishing is a release concern: the release workflow tags the image with the version it
   computes from the repository (backlog `F48`).
7. **CI builds both architectures on every change.** The `Host image` workflow builds the image for `linux/amd64` and
   `linux/arm64` without pushing it.
8. **Visual Studio keeps a debuggable stage.** Container fast mode debugs against the `base` stage, which needs a
   shell, so `base` stays on the full `aspnet` image. The production image never builds from it.

## Consequences

- The image refuses to start under its default `Production` environment until the deployment supplies production
  settings: `AllowedHosts`, `DeveloperKeys:Mode=None`, a protected key ring
  ([ADR-0177](0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md)), a database, and a tenant
  issuer.
- No shell means no `docker exec` debugging and no Dockerfile `HEALTHCHECK` that runs `curl`. Kubernetes probes
  `/health/live` and `/health/ready` over HTTP instead.
- An image build takes about two minutes on a standard runner. The Node and SDK stages are shared by both
  architectures, and the GitHub Actions cache keeps rebuilds of unchanged layers short.
- The assembly version inside the image omits git height and commit id. The image tag carries the release version.
