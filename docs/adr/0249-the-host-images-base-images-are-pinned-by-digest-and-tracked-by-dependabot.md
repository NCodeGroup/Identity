# 249. The host image's base images are pinned by digest and tracked by Dependabot

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

A tag such as `aspnet:10.0-noble-chiseled-extra` moves: the registry repoints it at each rebuild of the image. Two
builds of one commit can start from different base layers, and a compromised or broken tag reaches the host image
without anyone reviewing it (issue [#226](https://github.com/NCodeGroup/Identity/issues/226)).

A digest pins the layers, and two details of the tooling shape how it is written:

- Dependabot updates the `FROM` lines of a Dockerfile, and it cannot update a `FROM` that is built from an `ARG`.
- A reference of the form `image:tag@sha256:…` pulls the digest and ignores the tag beside it, so a tag or a version
  argument that is not the digest's own names an image that the build does not use.

The image's base images are a Node image for the login bundle, the .NET SDK for the publish, and the ASP.NET Core
runtime for the `base` and `final` stages
([ADR-0178](0178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md)). The Node major has
one source, `src/frontend/.node-version`
([ADR-0152](0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md)).

## Decision

**Each `FROM` of the host image names its tag and the digest of its multi-architecture index, and Dependabot moves the
digests every week.**

1. **Each `FROM` names the tag and the index digest.** The four base images are `node:26-trixie-slim`,
   `mcr.microsoft.com/dotnet/sdk:10.0-noble`, `mcr.microsoft.com/dotnet/aspnet:10.0-noble` and
   `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`, each followed by `@sha256:` and the digest of its
   multi-architecture index. The index resolves to the image of each platform, so `linux/amd64` and `linux/arm64` build
   from the one pin. The tag stays beside the digest because it tells a reader, and Dependabot, which image the digest
   is.
2. **No `ARG` builds a `FROM`.** The Dockerfile takes no build argument for a version. A version argument would make
   Dependabot unable to update the line, and the digest would override the version that it names.
3. **The Node major is a literal that the workflow checks.** The `Host image` workflow reads
   `src/frontend/.node-version` and fails unless the Dockerfile names `node:<major>-trixie-slim@sha256:` for that major,
   so the single pin of ADR-0152 stays the source and a `FROM` line need not read a file. The .NET major moves with the
   target framework: `sdk:10.0` and `aspnet:10.0` change when the target framework does.
4. **A weekly, grouped `docker` stream of Dependabot tracks the digests.** The `docker` entry of
   `.github/dependabot.yml` covers `/src/NCode.Identity.OpenId.Host`. It runs weekly, with at most five open pull
   requests, one group for version updates and one for security updates, and it ignores `version-update:semver-major`,
   so a runtime major moves deliberately. It runs although the version updates of the other ecosystems are paused
   (issue [#36](https://github.com/NCodeGroup/Identity/issues/36)): a digest pin receives the operating-system and runtime fixes only through this stream, so pausing it
   would leave the image on the layers of the day it was pinned.
5. **The `# syntax=docker/dockerfile:1` frontend stays unpinned.** It is the parser of the build and is never shipped in
   the image, and Dependabot cannot move a pin of it, so a digest would freeze the parser with nothing to update it.

## Options considered

- **Tags only.** Rejected: a tag moves, so builds of one commit differ, and a compromised or broken tag reaches the
  image unreviewed.
- **Digests without Dependabot.** Rejected: the image would stay on the layers of the day it was pinned and never
  receive a fix.
- **A `NODE_VERSION` argument beside a digest.** Rejected: the digest wins over the tag, so the argument would name a
  Node major that the build does not use.

## Consequences

- The base layers of a build are reproducible. A base image moves in a pull request that Dependabot opens and the
  `Host image` workflow builds, so the change is reviewed.
- A week's updates arrive as one pull request for the version updates, and security updates have a group of their own.
- A Node major bump edits `.node-version` and the Dockerfile together. The workflow fails when they differ, and
  Dependabot ignores a major.
- The `FROM` lines are longer than 120 columns, because a digest cannot wrap.
- **Breaking:** the Dockerfile takes no `NODE_VERSION` or `DOTNET_VERSION` build argument, so an argument passed for
  either is ignored.
- This implements issue [#226](https://github.com/NCodeGroup/Identity/issues/226).

## References

- [ADR-0152](0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md) — the Node major has one
  source.
- [ADR-0178](0178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md) — the host image.
- Code: `src/NCode.Identity.OpenId.Host/Dockerfile`, `.github/dependabot.yml`, `.github/workflows/host-image.yml`.
