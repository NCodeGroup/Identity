# Governance — NCode.Identity

NCode.Identity is an open-source authentication/identity library family for .NET. This document records how the
repository is owned, changed, versioned, and released. Governance is enforced in portable repository primitives (a
scripted Definition of Done, tag-driven releases, and — once shipped — protected branches) so it does not depend on
any one host feature.

## Ownership

- **Maintainers:** the **NCode Group**. Maintainers review and merge pull requests and cut releases.
- Ownership is a team fact. Path-scoped required reviewers (a `CODEOWNERS` file) are a future improvement, not needed
  while the maintainer set is small.

## Scope

- **In scope:** the OpenID Connect / OAuth 2.0 protocol model and server building blocks — JOSE/JWT, cryptographic
  secrets, the OpenID message/environment model, grant handling and endpoints, persistence abstractions, and DI
  composition.
- **Out of scope:** application-specific policy and business rules. Consumers build those on top of the library.

## Repositories and naming

The product spans the repositories below.

| Repository                     | Visibility | Holds |
| ------------------------------ | ---------- | ----- |
| `NCodeGroup/identity`          | Public     | The product: the NuGet packages (`src/`), the deployables (`hosts/`), the host and console images, the host chart (`charts/ncode-identity-host`), the web apps' pnpm workspace (`frontend/`), and the decisions and issues of the product and its platform ([ADR-1307](docs/decisions/1307-identity-platform-records-its-decisions-and-pending-work-in-this-repository.md)) |
| `NCodeGroup/identity-platform` | Public     | The reusable OpenTofu modules, Kustomize components and stacks, and the platform's live configuration for `lab` and `production` ([ADR-1132](docs/decisions/1132-platform-infrastructure-as-code-is-public-on-github.md), [ADR-1133](docs/decisions/1133-we-ship-and-run-the-same-deployment-artifacts.md)) |
| `NCodeGroup/identity-cloud`    | Private    | Only the managed offering's proprietary parts: billing and metering, sign-up, the customer portal, support tooling and private runbooks |

- **Public or private.** Code is public when a self-hoster could use it. Anything in `identity-cloud` that a
  self-hoster would want belongs in `identity` or `identity-platform`.
- **Decisions and issues.** The decisions and issues of `identity-platform` live in `identity`. `identity-cloud` keeps
  its private decisions and issues itself; a decision about a public contract it relies on is recorded in `identity`'s
  [`docs/decisions/`](docs/decisions/).
- **Naming.**
  - Repositories: lowercase kebab-case, product first: `identity`, `identity-platform`, `identity-cloud`.
  - NuGet packages: `NCode.Identity.*`
    ([ADR-1500](docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix.md)), under the
    `NCode` ID prefix the NCode Group holds on nuget.org.
  - The chart and the images: `ncode-identity-*`: the chart `ncode-identity-host`, the images
    `ghcr.io/ncodegroup/ncode-identity-host` and `ghcr.io/ncodegroup/ncode-identity-management-console`.
  - npm workspace packages: `@ncode/*`, every one `"private": true` and none published: the workspace root
    `@ncode/frontend`, the packages such as `@ncode/ui` and `@ncode/api`, and the apps `@ncode/app-*`.
  - Domains: `ncodeid.*`
    ([ADR-1495](docs/decisions/1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md)):
    `ncodeid.net` is the platform domain that carries every issuer, `ncodeid.com` the brand domain; the redirect-only
    `ncodeidentity.com` and `ncodeidentity.net` guard the name.
- **GitHub names.** Each repository's name on GitHub is the one the naming bullet gives, and GitHub resolves a
  repository name without regard to case, so a link to `https://github.com/NCodeGroup/Identity` reaches
  `NCodeGroup/identity`.

See [ADR-1502](docs/decisions/1502-code-a-self-hoster-could-use-is-public-and-the-products-repositories-and-artifacts-follow-one-naming-scheme.md).

## Change control

- A change starts as a **GitHub issue**. A change that affects the **public outcome** (semantics, high-level logic,
  features, the public API/surface, or the validation/security posture — anything a consumer could observe) should
  reach agreement on the issue and be recorded as an **ADR** under [`docs/decisions/`](docs/decisions/) before the pull request.
  Bug fixes, patches, and small changes go straight to a PR that references the issue.
- `main` is the single trunk and accepts changes only through a pull request. A repository ruleset requires the pull
  request, the `Definition of Done`, `Lint, typecheck, test, build`, `Lint and render the host chart`,
  `Build host image` and `End-to-end` status checks, resolved review threads, and linear history; it forbids
  force pushes and deletion, and nobody bypasses it. A pull request runs the fast gate: its `Definition of Done` check
  runs only the tests its change reaches. A merge goes through the **merge queue**, which runs the full gate on the
  merged result and squash-merges it, so a branch need not be up to date with `main`. An approval requirement is added
  when a second maintainer exists
  ([ADR-1261](docs/decisions/1261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md), which supersedes
  the unprotected trunk of [ADR-1125](docs/decisions/1125-trunk-based-development-on-main.md);
  [ADR-1263](docs/decisions/1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) for the gate
  tiers and the merge queue).
- The [scripted Definition of Done](build/dod.ps1) must pass — it is the single entrypoint that CI and developers run,
  each place in the gate tier that ADR-1263 assigns it. See [CONTRIBUTING](CONTRIBUTING.md).
- Versioning is **lockstep** across the whole family via Nerdbank.GitVersioning (one root `version.json`,
  [ADR-1003](docs/decisions/1003-central-package-management-and-lockstep-versioning.md)). From the first stable release,
  validation-affecting or otherwise breaking changes are **major** version bumps, and the public surface evolves
  backward-compatibly per [`docs/api-compatibility.md`](docs/api-compatibility.md)
  ([ADR-1005](docs/decisions/1005-public-api-surface-is-tracked-and-evolves-compatibly.md)); before it, a breaking change
  needs no major version, deprecation step or migration path
  ([project-template ADR-0015](docs/decisions/project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)).

## Releases, branches & tags

- **Trunk.** `main` is the single trunk; there is no long-lived integration branch
  ([project-template ADR-0004](docs/decisions/project-template/0004-trunk-based-development-on-the-default-branch.md)).
  Until the first stable release the version is an `-alpha` prerelease.
- **Release lines.** Each shipped `major.minor` is serviced from a long-lived `release/vX.Y` branch, cut with
  `nbgv prepare-release` (which creates the branch and bumps the trunk to the next prerelease). These branches match
  `publicReleaseRefSpec`, so their builds carry a clean version with no commit-height suffix. The ruleset
  `release-lines` (`release/v*`) protects them like `main`, but without a merge queue: every pull request runs the full
  gate on a branch up to date with its line
  ([ADR-1305](docs/decisions/1305-shipped-versions-are-serviced-from-release-lines-beside-the-trunk.md);
  [ADR-1263](docs/decisions/1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) for the gate).
- **Tags.** A release is an annotated (ideally signed) tag `vMAJOR.MINOR.PATCH` (prereleases `-rc.N` / `-alpha`) on a
  release line. A tag is immutable — one tag = one published package set; the ruleset `release-tags` forbids deleting or
  moving a `v*` tag. Only a **major** is gated behind a release candidate; minors and patches ship straight to GA.
  The template's `Release` workflow (`.github/workflows/tag-release.yml`) runs on each push to `main` and tags
  nothing, because the [Landing](AGENTS.md#landing) section of `AGENTS.md` says `release: none`.
- **Servicing** is upstream-first. A fix merges to `main` first; a backport pull request cherry-picks it
  (`git cherry-pick -x`) onto a branch `backport/<pr>-vX.Y` cut from `release/vX.Y` and targets that line. Label the
  fix's pull request `backport vX.Y`, before or after it merges, and the `Backport` workflow
  (`.github/workflows/backport.yml`, `build/backport.ps1`) opens that pull request, or comments with the commands that
  finish it by hand when the cherry-pick conflicts; whoever cuts a release line creates its label
  (`gh label create "backport vX.Y"`). The workflow opens its pull requests as the GitHub App that the variable
  `BACKPORT_APP_CLIENT_ID` and the secret `BACKPORT_APP_PRIVATE_KEY` name (contents and pull requests: read and write),
  so their checks run; without the app it uses the workflow token, which starts no checks, and the pull request says
  to close and reopen it. A fix that cannot apply to `main` lands directly on the release line by pull request, whose
  description states why. A patch release is an annotated `vX.Y.Z` tag on the release line; a hotfix follows the same
  flow, expedited (ADR-1263).
- **Binary compatibility.** `AssemblyVersion` is pinned to `major` precision (all `1.*` share `1.0.0.0`), so patch and
  minor upgrades are drop-in within a major and different majors load side by side; `InformationalVersion` /
  `FileVersion` still carry the exact build.

## Publishing

- Packages (`.nupkg`) and portable symbols (`.snupkg`, [ADR-1007](docs/decisions/1007-provenance-sourcelink-and-symbols.md))
  are published to [nuget.org](https://www.nuget.org). Only protected `main` / `release/*` / `v*` refs publish.
- Release packages should be signed and RFC-3161 timestamped so signatures outlive the certificate; verify with
  `dotnet nuget verify`. Prereleases may publish unsigned.
- A prerelease publishes through the `Publish prerelease packages` workflow
  (`.github/workflows/publish-prerelease.yml`) from a `v*.*.*-*` tag that names a commit on `main` or a release line, or
  from a manual run on `main`: bump the prerelease version in `version.json`, merge it, then tag `v<version>` or run the
  workflow, and approve the `nuget` environment. Its publishing job holds only a short-lived key from nuget.org trusted
  publishing, and it refuses a version already published
  ([ADR-1302](docs/decisions/1302-prerelease-packages-publish-from-a-prerelease-tag-or-a-manual-run-on-main-through-trusted-publishing.md)).

## Definition of Done

The [`build/dod.ps1`](build/dod.ps1) script is the contract. It enforces:

- **CSharpier** formatting (`dotnet csharpier check .`).
- Restore + build on **net10**, with warnings-as-errors and vulnerability advisories as errors in CI.
- **Banned APIs** (`BannedApiAnalyzers`, `RS0030`) from `BannedSymbols.txt` — no `Newtonsoft.Json`, no sync-over-async —
  and from `BannedSymbols.Encoding.txt` in every project, tests included — no lenient text encodings (ADR-1266).
- **Public API surface guard** (`PublicApiAnalyzers`) — the declared surface in each packable project's
  `PublicAPI.{Shipped,Unshipped}.txt`.
- Tests with coverage — the full suite in the full gate; a pull request into `main` runs the affected tests (ADR-1263).
- Deterministic, source-linked builds.
