# 133. We ship and run the same deployment artifacts

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

NCode.Identity is both an open-source library family with a deployable host
([ADR-0130](0130-distinct-names-for-host-local-dev-orchestrator-and-library.md)) and a hosted platform
([ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md)) whose infrastructure code is public
([ADR-0132](0132-platform-infrastructure-as-code-is-public-on-github.md)). That raises a recurring question: does the
platform run on private, platform-only deployment code, or on the same artifacts a self-hoster would use?

Forces:

- **Divergence is the default failure.** Projects that run a hosted edition on private infrastructure code end up with a
  well-tested "cloud" path and a second-class self-hosted path that nobody exercises.
- **Self-hosting is a differentiator.** Auth0 is SaaS-only; an Auth0-parity-plus product
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)) that can also be self-hosted on the exact artifacts the
  vendor runs is a stronger offer.
- **Support budget.** Every published deployment module is a product: versioned, documented, and triaged against other
  people's environments. A small maintainer team can afford only a few.
- **Pre-release posture.** Nothing is stable yet, so the layout can be set correctly from the start at almost no cost,
  while publication can wait until the artifacts settle.

## Decision

**The hosted platform consumes only published deployment artifacts — the same ones offered to self-hosters — built as
small components composed into opinionated stacks, and the supported set is limited to what the platform itself runs.**

1. **Helm chart — the primary, vendor-neutral artifact.** The chart for `NCode.Identity.OpenId.Host` lives in the library
   repository with the host and its Dockerfile, at `charts/ncode-identity-host`
   ([ADR-0179](0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)), because it
   encodes the host's configuration surface (settings, ports, probes) and must change atomically with it. It is versioned and released with the host image, and published
   as a signed OCI chart on GHCR with provenance.
2. **Layered building blocks** in `NCodeGroup/identity-platform`, so users can take a single component, a whole stack,
   or a stack as a template to fork:

   | Layer | Path | What it is |
   | ----- | ---- | ---------- |
   | Component modules | `modules/` | OpenTofu, one cloud concern each — e.g. `cloudflare-zone`, `cloudflare-email-routing`, `aws-state-backend`, `aws-backup-bucket`, `aws-github-oidc`, `akamai-lke`, `akamai-postgres`, `oci-k3s`, `flux-bootstrap` |
   | In-cluster components | `kubernetes/components/` | Kustomize bases / Helm releases — `traefik`, `cert-manager`, `valkey`, `identity` (the chart plus its schema-migration job); usable with Flux, Argo CD, or `kubectl apply -k` |
   | Stacks (composition roots) | `stacks/`, `kubernetes/stacks/` | The full opinionated platform per vendor — initially `akamai` (production) and `oci` (lab) — wiring components together; still generic, configured through inputs |
   | Live configuration | `live/`, `clusters/` | The hosted platform's own instances (`production`, `lab`): providers, backends, and values |

   **Component rules:** a component owns one concern; it declares `required_providers` but never configures a provider
   or backend (its caller does); it takes its dependencies as explicit inputs and exposes what others need as outputs —
   no cross-component data lookups; it is secure by default; and it ships a README, an example, and `tofu test` tests with
   mocked providers. A component's variables and outputs are its public API.
3. **Versioning.** Every release is versioned and immutable:
   - The whole repository releases with one semver version, in lockstep like the library's packages
     ([ADR-0003](0003-central-package-management-and-lockstep-versioning.md)); `0.x` until stable. Stacks reference
     components by relative path, since they ship in the same release. Everything is Apache-2.0.
   - **Breaking** means any change a consumer must act on: removing or renaming a variable or output, a new required
     input, or a change that **forces a resource to be replaced or destroyed** even when the interface is unchanged.
     Refactors use `moved` / `removed` blocks to avoid replacement; release notes list breaking changes and migrations.
   - **Immutable releases:** tags are protected by a ruleset (no deletion or move), and each release also publishes the
     OpenTofu modules and the Kustomize bases as **signed OCI artifacts on GHCR** with provenance, so consumers can pin
     by digest and Flux can verify signatures before applying.
   - Components declare provider **version ranges**; exact provider versions are pinned only in `live/` through the
     lock file. Renovate proposes release bumps for `live/` and `clusters/`.
4. **The member rule.** `live/` and `clusters/` reference stacks and components **only by released version** — OpenTofu
   module sources and Flux sources pinned to a release (by digest where the source supports it) — never by local path;
   a CI check fails on any local source there. A release rolls through the lab, then production, before it is announced.
5. **Platform specifics stay in `live/` and `clusters/`.** Domains, hostnames, accounts, and SSM paths are inputs; no
   component or stack carries a `nidentity.io` default.
6. **Support tiers.** Supported: the chart, the cloud-neutral in-cluster components, and the components and stacks for
   vendors the platform runs. Anything else is a community example without support. A vendor's components become
   supported only when the platform runs on that vendor, and are deprecated if it stops.

## Options considered

- **Private, platform-only infrastructure code.** Least effort now, but the self-hosted path would go untested and
  diverge. Rejected.
- **Modules for every popular vendor.** Broadest reach, but an unbounded support burden on code the maintainers never
  run. Rejected in favor of "only what we run".
- **One monolithic stack module per vendor.** Simplest to build, but all-or-nothing: a user who already has a cluster or
  a database can't reuse the rest. Rejected in favor of components composed by stacks.
- **Per-component version tags.** Precise, but many release trains for a small team and a compatibility matrix between
  components. Rejected in favor of one lockstep tag.
- **In-cluster components as OpenTofu `helm_release` resources.** One tool end to end, but it puts in-cluster state in
  OpenTofu instead of GitOps ([ADR-0132](0132-platform-infrastructure-as-code-is-public-on-github.md)); Kustomize bases
  stay usable with any GitOps tool. Rejected.
- **A separate repository for reusable components.** Cleaner history for their consumers, but double the repository
  overhead for a small team; tag-pinned consumption from `live/` already enforces the member rule. Deferred; revisit if
  consumers need independent history.
- **The chart in the platform repository.** Would split every host configuration change across two repositories.
  Rejected.
- **Publishing to the OpenTofu registry now.** Requires per-module repository naming conventions; git-tag sources
  suffice before 1.0. Deferred.

## Consequences

- Every platform deployment exercises the published artifacts; platform incidents improve what self-hosters run.
- Components must be written generically from the start (inputs instead of hard-coded values), with tests, a release
  process, and release notes; stacks stay thin wiring over them.
- Self-hosting documentation becomes part of the product, and issues from other environments must be triaged within the
  supported tiers.
- Work is tracked in the backlog as `F48`, building on the host deployment artifacts in `F46`.

## References

- [ADR-0132](0132-platform-infrastructure-as-code-is-public-on-github.md) — the public `NCodeGroup/identity-platform`
  repository and its guardrails.
- [Hosting analysis §11](../hosting-analysis.md#11-iac-and-delivery) — delivery layout.
