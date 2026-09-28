# Governance — NCode.Identity

NCode.Identity is an open-source authentication/identity library family for .NET. This document records how the
repository is owned, changed, versioned, and released. Governance is enforced in portable repository primitives
(protected branches, a scripted Definition of Done, tag-driven releases) so it does not depend on any one host feature.

## Ownership

- **Maintainers:** the **NCode Group**. Maintainers review and merge pull requests and cut releases.
- Ownership is a team fact. Path-scoped required reviewers (a `CODEOWNERS` file) are a future improvement, not needed
  while the maintainer set is small.

## Scope

- **In scope:** the OpenID Connect / OAuth 2.0 protocol model and server building blocks — JOSE/JWT, cryptographic
  secrets, the OpenID message/environment model, grant handling and endpoints, persistence abstractions, and DI
  composition.
- **Out of scope:** application-specific policy and business rules. Consumers build those on top of the library.

## Change control

- A change starts as a **GitHub issue**. A change that affects the **public outcome** (semantics, high-level logic,
  features, the public API/surface, or the validation/security posture — anything a consumer could observe) should
  reach agreement on the issue and be recorded as an **ADR** under [`docs/adr/`](docs/adr/) before the pull request.
  Bug fixes, patches, and small changes go straight to a PR that references the issue.
- `main` is protected: changes land via pull request only, requiring at least one maintainer approval. No self-approval;
  stale approvals are dismissed on new pushes. Day-to-day work happens on `dev` and short-lived feature branches.
- The [scripted Definition of Done](build/dod.ps1) must pass — it is the single entrypoint invoked identically by CI
  and locally. See [CONTRIBUTING](CONTRIBUTING.md).
- Versioning is **lockstep** across the whole family via Nerdbank.GitVersioning (one root `version.json`,
  [ADR-0003](docs/adr/0003-central-package-management-and-lockstep-versioning.md)). Validation-affecting or otherwise
  breaking changes are **major** version bumps. Once GA, evolve the public surface backward-compatibly per
  [`docs/api-compatibility.md`](docs/api-compatibility.md)
  ([ADR-0005](docs/adr/0005-public-api-surface-is-tracked-and-evolves-compatibly.md)).

## Releases, branches & tags

- **Trunk.** `main` is the released line; `dev` is the integration branch for in-progress work. Until the first stable
  release the version is an `-alpha` prerelease.
- **Release lines.** Each shipped `major.minor` is serviced from a long-lived `release/vX.Y` branch, cut with
  `nbgv prepare-release` (which creates the branch and bumps the trunk to the next prerelease). These branches match
  `publicReleaseRefSpec`, so their builds carry a clean version with no commit-height suffix.
- **Tags.** A release is an annotated (ideally signed) tag `vMAJOR.MINOR.PATCH` (prereleases `-rc.N` / `-alpha`) on a
  release line. A tag is immutable — one tag = one published package set. Only a **major** is gated behind a release
  candidate; minors and patches ship straight to GA.
- **Binary compatibility.** `AssemblyVersion` is pinned to `major` precision (all `1.*` share `1.0.0.0`), so patch and
  minor upgrades are drop-in within a major and different majors load side by side; `InformationalVersion` /
  `FileVersion` still carry the exact build.

## Publishing

- Packages (`.nupkg`) and portable symbols (`.snupkg`, [ADR-0007](docs/adr/0007-provenance-sourcelink-and-symbols.md))
  are published to [nuget.org](https://www.nuget.org). Only protected `main` / `release/*` / `v*` refs publish.
- Release packages should be signed and RFC-3161 timestamped so signatures outlive the certificate; verify with
  `dotnet nuget verify`. Prereleases from feature branches may publish unsigned.

## Definition of Done

The [`build/dod.ps1`](build/dod.ps1) script is the contract. It enforces:

- **CSharpier** formatting (`dotnet csharpier check .`).
- Restore + build on **net10**, with warnings-as-errors and vulnerability advisories as errors in CI.
- **Banned APIs** (`BannedApiAnalyzers`, `RS0030`) from `BannedSymbols.txt` — no `Newtonsoft.Json`, no sync-over-async.
- **Public API surface guard** (`PublicApiAnalyzers`) — the declared surface in each packable project's
  `PublicAPI.{Shipped,Unshipped}.txt`.
- Full test suite with coverage.
- Deterministic, source-linked builds.
