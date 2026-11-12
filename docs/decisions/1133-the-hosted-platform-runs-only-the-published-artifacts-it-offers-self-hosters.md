# 1133. The hosted platform runs only the published artifacts it offers self-hosters

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

NCode.Identity is both an open-source library family with a deployable host
([ADR-1501](1501-a-projects-top-level-folder-and-name-say-whether-it-ships-and-what-it-is.md)) and a hosted platform
([ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md)) whose infrastructure code is public
([ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md)). That
raises a recurring question: does the platform run on private, platform-only deployment code, or on the same artifacts a
self-hoster would use?

Forces:

- **Divergence is the default failure.** Projects that run a hosted edition on private infrastructure code end up with a
  well-tested "cloud" path and a second-class self-hosted path that nobody exercises.
- **Self-hosting is a differentiator.** Auth0 is SaaS-only; an Auth0-parity-plus product
  ([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)) that
  can also be self-hosted on the exact artifacts the vendor runs is a stronger offer.
- **Support budget.** Every published deployment module is a product: versioned, documented, and triaged against other
  people's environments. A small maintainer team can afford only a few.
- **Pre-release posture.** Nothing is stable yet, so the layout can be set correctly from the start at almost no cost,
  while publication can wait until the artifacts settle.

## Decision

**The hosted platform consumes only published deployment artifacts, the same ones offered to self-hosters, built as
small components composed into opinionated stacks, and the supported set is limited to what the platform itself runs.**

1. **Helm chart: the primary, vendor-neutral artifact.** The chart for `NCode.Identity.OpenId.Host` is
   `charts/ncode-identity-host`. It lives in `identity` with the host and its Dockerfile
   ([ADR-1502](1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md)),
   because it encodes the host's configuration surface (settings, ports, probes) and must change atomically with it
   ([ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)). It is
   versioned with the host image and released as a signed OCI chart on GHCR with provenance.
2. **Layered building blocks** in `NCodeGroup/identity-platform`, so users can take a single component, a whole stack,
   or a stack as a template to fork:

   | Layer | Path | What it is |
   | ----- | ---- | ---------- |
   | Component modules | `modules/` | OpenTofu, one concern each, e.g. `cloudflare-zone`, `aws-state-backend`, `aws-github-oidc`, `flux-bootstrap` |
   | In-cluster components | `kubernetes/components/` | Kustomize bases: `traefik`, `cert-manager`, `valkey`, `identity` |
   | Stacks (composition roots) | `stacks/`, `kubernetes/stacks/` | Per-vendor composition roots that wire components together |
   | Live configuration | `live/`, `clusters/` | The platform's own instances |

   **Component rules:** a component owns one concern. It declares `required_providers` but configures no provider and
   no backend. It takes its dependencies as inputs and exposes what others need as outputs, with no cross-component
   lookups. It is secure by default. It ships a README, an example and `tofu test` tests with mocked providers. Its
   variables and outputs are its public API.
3. **Versioning.**
   - The repository releases with one lockstep semver version, like the library's packages
     ([ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md)),
     `0.x` until stable, under Apache-2.0. Stacks reference components by relative path, since they ship in the same
     release. Tags are protected and immutable.
   - Each release publishes the modules and the Kustomize bases as signed OCI artifacts on GHCR with provenance, so
     consumers pin by digest and Flux verifies signatures.
   - A major version is any change a consumer must act on: a removed or renamed variable or output, a new required
     input, or a forced resource replacement. Refactors use `moved` and `removed` blocks.
   - Components declare provider version ranges; exact versions are pinned only in `live/` through the lock file, and
     Renovate proposes release bumps.
4. **The member rule.** `live/` and `clusters/` reference stacks and components only by released version, pinned by
   digest where the source supports it. A CI check fails any local source there. A release rolls through the lab and
   then production.
5. **Platform specifics stay in `live/` and `clusters/`.** Domains, hostnames, accounts and SSM paths are inputs, and no
   component or stack carries a default for any of the platform's domains
   ([ADR-1495](1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md)).
6. **Support tiers.** Supported: the chart, the cloud-neutral in-cluster components, and the components for the vendors
   the platform runs. A vendor's components are supported only while the platform runs on it. Everything else is an
   unsupported community example.

## Options considered

- **Private, platform-only infrastructure code.** Least effort, but the self-hosted path goes untested and diverges.
  Rejected.
- **Modules for every vendor.** Broadest reach, but an unbounded support burden on code the maintainers never run.
  Rejected in favor of supporting only what the platform runs.
- **One monolithic stack per vendor.** Simplest to build, but all-or-nothing: a user with a cluster or a database cannot
  reuse the rest. Rejected in favor of components composed by stacks.
- **Per-component tags.** Precise, but many release trains and a compatibility matrix for a small team. Rejected in favor
  of one lockstep tag.
- **`helm_release` resources for in-cluster components.** One tool end to end, but it puts in-cluster state in OpenTofu
  instead of GitOps; Kustomize bases stay usable with any GitOps tool. Rejected.
- **A separate components repository.** Cleaner history for consumers, but double the repository overhead for a small
  team; tag-pinned consumption from `live/` already enforces the member rule. Deferred until consumers need independent
  history.
- **The OpenTofu registry before 1.0.** It requires per-module repository naming conventions; git-tag sources and OCI
  artifacts suffice. Rejected until 1.0.
- **Chosen:** published, signed components composed into stacks, with the platform consuming only what it publishes.

## Consequences

- Every platform deployment exercises the published artifacts, so platform incidents improve what self-hosters run.
- Components are written generically from the start, with inputs instead of hard-coded values, tests and a release
  process; stacks stay thin wiring over them.
- Self-hosting documentation is part of the product, and issues from other environments are triaged within the
  supported tiers.
- Publishing the chart and the artifacts to GHCR is tracked in
  [#87](https://github.com/NCodeGroup/identity/issues/87).

## References

- [ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md): the public
  `NCodeGroup/identity-platform` repository and its guardrails.
- [Hosting analysis §11](../hosting-analysis.md#11-iac-and-delivery): delivery layout.
- Issue [#87](https://github.com/NCodeGroup/identity/issues/87).
