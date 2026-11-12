# 1502. Code a self-hoster could use is public, and the product's repositories and artifacts follow one naming scheme

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** NCode Group
- **Amends:** [ADR-1064](1064-frontend-presentation-platform.md) (the frontend platform stays in this repository)
- **Refines:** [ADR-1307](1307-identity-platform-records-its-decisions-and-pending-work-in-this-repository.md)

## Context

The product has three audiences: consumers of the packages, self-hosters of the host and the platform, and the operators
of the managed offering. A repository's visibility and name tell a reader which part of the product is open, and a
package ID or an image name is permanent once it is published.

## Decision

**Code is public when a self-hoster could use it, and the product's repositories, packages, charts, images, npm
packages and domains follow one naming scheme.**

| Repository                     | Visibility | Holds |
| ------------------------------ | ---------- | ----- |
| `NCodeGroup/identity`          | Public     | The product: the NuGet packages (`src/`), the deployables (`hosts/`), the host and console images, the host chart (`charts/ncode-identity-host`), the web apps' pnpm workspace (`frontend/`), and the decisions and issues of the product and its platform ([ADR-1307](1307-identity-platform-records-its-decisions-and-pending-work-in-this-repository.md)) |
| `NCodeGroup/identity-platform` | Public     | The reusable OpenTofu modules, Kustomize components and stacks, and the platform's live configuration for `lab` and `production` ([ADR-1132](1132-platform-infrastructure-as-code-is-public-on-github.md), [ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md)) |
| `NCodeGroup/identity-cloud`    | Private    | Only the managed offering's proprietary parts: billing and metering, sign-up, the customer portal, support tooling and private runbooks |

1. **The repositories.** The three repositories are the table above. `identity-cloud` is created with the managed
   offering's first proprietary code.
2. **The product's repository.** `identity` is not a library-only repository: the chart and the images change with the
   host, so they live beside it ([ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md) decision 1). The
   packages, hosts, images and chart version in lockstep from one `version.json`
   ([ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md)), and the console image builds `frontend/`,
   whose private packages are never published.
   So `frontend/` and every package stay in `identity`, at the first stable release and after it.
3. **Public or private.** Code is public when a self-hoster could use it. Anything in `identity-cloud` that a
   self-hoster would want belongs in `identity` or `identity-platform`.
4. **Decisions and issues.** [ADR-1307](1307-identity-platform-records-its-decisions-and-pending-work-in-this-repository.md)
   holds for `identity-platform`: its decisions and issues live in `identity`. `identity-cloud` keeps its private
   decisions and issues itself; a decision about a public contract it relies on is recorded in `identity`'s
   `docs/decisions/`.
5. **Naming.**
   - Repositories: lowercase kebab-case, product first: `identity`, `identity-platform`, `identity-cloud`.
   - NuGet packages: `NCode.Identity.*`
     ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)), under the `NCode` ID
     prefix the NCode Group holds on nuget.org.
   - The chart and the images: `ncode-identity-*`: the chart `ncode-identity-host`, the images
     `ghcr.io/ncodegroup/ncode-identity-host` and `ghcr.io/ncodegroup/ncode-identity-management-console`.
   - npm workspace packages: `@ncode/*`, every one `"private": true` and none published: the workspace root
     `@ncode/frontend`, the packages such as `@ncode/ui` and `@ncode/api`, and the apps `@ncode/app-*`.
   - Domains: `ncodeid.*`
     ([ADR-1495](1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md)):
     `ncodeid.net` is the platform domain that carries every issuer, `ncodeid.com` the brand domain; the redirect-only
     `ncodeidentity.com` and `ncodeidentity.net` guard the name.
6. **GitHub names.** Each repository's name on GitHub is the one decision 5 gives, and every link and reference spells
   it that way, such as `https://github.com/NCodeGroup/identity` and `NCodeGroup/identity#760`. GitHub resolves a
   repository name without regard to case, so a link in any capitalization reaches the repository.

## Options considered

- **A library-only repository, with the chart and images in `identity-platform`.** Rejected: every host configuration
  change would span two repositories ([ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md)).
- **One private repository for the managed offering's infrastructure and code.** Rejected: self-hosters need the
  components, and [ADR-1132](1132-platform-infrastructure-as-code-is-public-on-github.md) makes the infrastructure
  public.
- **Separate repositories for the packages and the frontend at the first stable release.** Rejected: the packages
  version in lockstep from one `version.json`, and the console image builds `frontend/`.
- **Repository names that repeat the organization, such as `NCodeGroup/ncode-identity`.** Rejected: the organization
  already names the company.

## Consequences

- A self-hoster finds everything it runs in the two public repositories.
- A proprietary part of the managed offering consumes only `identity`'s public contracts, and a change to such a
  contract is decided in `identity`.
- Every link and reference spells a repository as its GitHub name, such as `NCodeGroup/identity`; GitHub resolves a
  link in any capitalization to the same repository.

## References

- Issue [#760](https://github.com/NCodeGroup/identity/issues/760).
- [ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md),
  [ADR-1064](1064-frontend-presentation-platform.md),
  [ADR-1132](1132-platform-infrastructure-as-code-is-public-on-github.md),
  [ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md),
  [ADR-1307](1307-identity-platform-records-its-decisions-and-pending-work-in-this-repository.md),
  [ADR-1495](1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md),
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1501](1501-a-projects-top-level-folder-and-name-say-whether-it-ships-and-what-it-is.md)
