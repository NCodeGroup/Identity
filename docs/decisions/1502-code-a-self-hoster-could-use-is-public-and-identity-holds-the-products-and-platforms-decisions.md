# 1502. Code a self-hoster could use is public, and identity holds the product's and platform's decisions

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0010](project-template/0010-a-change-consumers-can-observe-is-decided-before-it-is-built.md)

## Context

The product has three audiences: consumers of the packages, self-hosters of the host and the platform, and the operators
of the managed offering. A repository's visibility and name tell a reader which part of the product is open, and a
package ID or an image name is permanent once it is published.

[`NCodeGroup/identity-platform`](https://github.com/NCodeGroup/identity-platform) holds the hosted platform's
infrastructure as code in a public repository of its own
([ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md)). It is made from the same project template as
this repository. The template makes each repository the home of its own decisions
([project-template ADR-0010](project-template/0010-a-change-consumers-can-observe-is-decided-before-it-is-built.md))
and pending work ([project-template ADR-0007](project-template/0007-pending-work-is-tracked-as-issues.md)), and its
merge-method record rules out squash merges
([project-template ADR-0013](project-template/0013-pull-requests-merge-with-a-merge-commit-or-by-rebasing.md)). The
platform's decisions build on the product's and cite them, and its work is planned beside the product's.

## Decision

**Code is public when a self-hoster could use it, and `identity` holds the decisions and issues of the product and its
platform.** Anything in the private repository that a self-hoster would want belongs in a public one.

| Repository                     | Visibility | Holds |
| ------------------------------ | ---------- | ----- |
| `NCodeGroup/identity`          | Public     | The NuGet packages (`src/`), the deployables (`hosts/`), the host and console images, the host chart (`charts/ncode-identity-host`), the web apps' pnpm workspace (`frontend/`), and the decisions and issues of the product and its platform |
| `NCodeGroup/identity-platform` | Public     | The reusable OpenTofu modules, Kustomize components and stacks, and the live configuration for `lab` and `production` ([ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md), [ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md)) |
| `NCodeGroup/identity-cloud`    | Private    | Only the managed offering's proprietary parts: billing and metering, sign-up, the customer portal, support tooling and private runbooks |

1. **The repositories.** The three repositories are the table above. `identity-cloud` is created with the managed
   offering's first proprietary code.
2. **The product's repository.** `identity` is not library-only: the chart and the images change with the host, so they
   live beside it ([ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md)). The packages, hosts, images and
   chart version in lockstep from one `version.json`
   ([ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md)),
   and the console image builds `frontend/`, whose private packages are never published. So `frontend/` and every
   package stay in `identity`, at the first stable release and after it.
3. **Decisions.** `identity-platform` keeps no decision records and no issues of its own. Its `AGENTS.md` lists that as
   overriding project-template ADR-0010, ADR-0007 and ADR-0013 there, and its `docs/decisions/` holds only the
   template's records. A change `identity-platform`'s consumers can observe starts as an issue in `identity` and is
   recorded as an ADR in `identity`'s `docs/decisions/` before or with the platform pull request that builds it, so
   such a change takes two pull requests. `/analyze-decision` is not used there.
4. **Pending work.** `identity-platform`'s pending work is issues in `identity`
   ([ADR-1306](1306-pending-work-is-tracked-as-github-issues-linked-by-address-in-repository-files.md)). Its
   `/ship-plan` files follow-ups here (`follow-ups: NCodeGroup/identity`), and its pull requests end with
   `Closes NCodeGroup/identity#<number>` or `Refs NCodeGroup/identity#<number>`, so an issue here may be closed by a
   platform pull request.
5. **Merge method.** `identity-platform`'s `main` takes only squash merges with linear history, for the reasons this
   repository squash-merges
   ([ADR-1261](1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md)):
   the pull request's conventional-commit title and description become the one commit, and agent attribution is
   avoided in both ([project-template ADR-0018](project-template/0018-agent-attribution-is-avoided-not-enforced.md)).
6. **The managed offering.** `identity-cloud` keeps its private decisions and issues itself. A decision about a public
   contract it relies on is recorded in `identity`, so a proprietary part consumes only `identity`'s public contracts.
7. **Naming.**
   - Repositories: lowercase kebab-case, product first: `identity`, `identity-platform`, `identity-cloud`.
   - NuGet packages: `NCode.Identity.*`
     ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)),
     under the `NCode` ID prefix the NCode Group holds on nuget.org.
   - The chart and the images: the chart `ncode-identity-host`, the images `ghcr.io/ncodegroup/ncode-identity-host` and
     `ghcr.io/ncodegroup/ncode-identity-management-console`.
   - npm workspace packages: `@ncode/*`, every one `"private": true` and none published: the root `@ncode/frontend`,
     packages such as `@ncode/ui` and `@ncode/api`, and the apps `@ncode/app-*`.
   - Domains: the domains are
     [ADR-1495](1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md)'s.
8. **GitHub names.** Every link and reference spells a repository as its GitHub name, such as
   `https://github.com/NCodeGroup/identity` and `NCodeGroup/identity#760`. GitHub resolves a repository name without
   regard to case, so a link in any capitalization reaches the repository.

## Options considered

- **A library-only repository, with the chart and images in `identity-platform`.** Rejected: every host configuration
  change would span two repositories ([ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md)).
- **One private repository for the managed offering.** Rejected: self-hosters need the components, and
  [ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md) makes the infrastructure public.
- **Separate repositories for the packages and the frontend at the first stable release.** Rejected: the packages
  version in lockstep from one `version.json`, and the console image builds `frontend/`.
- **Repository names that repeat the organization, such as `NCodeGroup/ncode-identity`.** Rejected: the organization
  already names the company.
- **Decisions and issues in `identity-platform`, as the template sets out.** Rejected: the platform's decisions build on
  the product's, so most records would link across repositories, and the work would be split across two queues that
  `/triage-issues` and `/run-issues` read separately.
- **Decisions here and issues in `identity-platform`.** Rejected: every issue would link a record in another
  repository, and a pull request here could not close the item it completes.
- **Decisions and issues both in `identity` (chosen).** One index of decisions and one work queue for the product and
  the platform that hosts it.

## Consequences

- A self-hoster finds everything it runs in the two public repositories.
- A proprietary part of the managed offering consumes only `identity`'s public contracts, and a change to such a
  contract is decided in `identity`.
- A platform change that needs a decision takes two pull requests: the record in `identity`, then the change in
  `identity-platform` that cites it.
- An issue in `identity` may be completed by a pull request in `identity-platform`, which closes it with
  `Closes NCodeGroup/identity#<number>`.
- Every link and reference spells a repository as its GitHub name; GitHub resolves a link in any capitalization to the
  same repository.

## References

- Issues [#760](https://github.com/NCodeGroup/identity/issues/760) and
  [#438](https://github.com/NCodeGroup/identity/issues/438).
- `identity-platform`'s [`AGENTS.md`](https://github.com/NCodeGroup/identity-platform/blob/main/AGENTS.md), which lists
  the overrides.
- [ADR-1003](1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md),
  [ADR-1064](1064-frontend-presentation-platform.md),
  [ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md),
  [ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md),
  [ADR-1261](1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md),
  [ADR-1306](1306-pending-work-is-tracked-as-github-issues-linked-by-address-in-repository-files.md),
  [ADR-1495](1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md),
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1501](1501-a-projects-top-level-folder-and-name-say-whether-it-ships-and-what-it-is.md)
