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

## Change control

- A change starts as a **GitHub issue**. A change that affects the **public outcome** (semantics, high-level logic,
  features, the public API/surface, or the validation/security posture — anything a consumer could observe) should
  reach agreement on the issue and be recorded as an **ADR** under [`docs/decisions/`](docs/decisions/) before the pull request.
  Bug fixes, patches, and small changes go straight to a PR that references the issue.
- `main` is the single trunk and accepts changes only through a pull request. A repository ruleset requires the pull
  request, the `Definition of Done`, `Lint, typecheck, test, build`, `Lint and render the host chart`,
  `Build host image` and `Commit attribution` status checks, resolved review threads, and linear history; it forbids
  force pushes and deletion, and nobody bypasses it. A pull request runs the fast gate: its `Definition of Done` check
  runs only the tests its change reaches. A merge goes through the **merge queue**, which runs the full gate on the
  merged result and squash-merges it, so a branch need not be up to date with `main`. An approval requirement is added
  when a second maintainer exists
  ([ADR-0261](docs/decisions/0261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md), which supersedes
  the unprotected trunk of [ADR-0125](docs/decisions/0125-trunk-based-development-on-main.md);
  [ADR-0263](docs/decisions/0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) for the gate
  tiers and the merge queue;
  [ADR-0128](docs/decisions/0128-commits-are-attributed-to-people-not-agents.md) for `Commit attribution`).
- The [scripted Definition of Done](build/dod.ps1) must pass — it is the single entrypoint that CI and developers run,
  each place in the gate tier that ADR-0263 assigns it. See [CONTRIBUTING](CONTRIBUTING.md).
- Versioning is **lockstep** across the whole family via Nerdbank.GitVersioning (one root `version.json`,
  [ADR-0003](docs/decisions/0003-central-package-management-and-lockstep-versioning.md)). Validation-affecting or otherwise
  breaking changes are **major** version bumps. Once GA, evolve the public surface backward-compatibly per
  [`docs/api-compatibility.md`](docs/api-compatibility.md)
  ([ADR-0005](docs/decisions/0005-public-api-surface-is-tracked-and-evolves-compatibly.md)).

## Releases, branches & tags

- **Trunk.** `main` is the single trunk and the released line; there is no long-lived integration branch
  ([ADR-0125](docs/decisions/0125-trunk-based-development-on-main.md)). Until the first stable release the version is an
  `-alpha` prerelease.
- **Release lines.** Each shipped `major.minor` is serviced from a long-lived `release/vX.Y` branch, cut with
  `nbgv prepare-release` (which creates the branch and bumps the trunk to the next prerelease). These branches match
  `publicReleaseRefSpec`, so their builds carry a clean version with no commit-height suffix. The ruleset
  `release-lines` (`release/v*`) protects them like `main`, but without a merge queue: every pull request runs the full
  gate on a branch up to date with its line
  ([ADR-0263](docs/decisions/0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)).
- **Tags.** A release is an annotated (ideally signed) tag `vMAJOR.MINOR.PATCH` (prereleases `-rc.N` / `-alpha`) on a
  release line. A tag is immutable — one tag = one published package set; the ruleset `release-tags` forbids deleting or
  moving a `v*` tag. Only a **major** is gated behind a release candidate; minors and patches ship straight to GA.
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
  flow, expedited (ADR-0263).
- **Binary compatibility.** `AssemblyVersion` is pinned to `major` precision (all `1.*` share `1.0.0.0`), so patch and
  minor upgrades are drop-in within a major and different majors load side by side; `InformationalVersion` /
  `FileVersion` still carry the exact build.

## Publishing

- Packages (`.nupkg`) and portable symbols (`.snupkg`, [ADR-0007](docs/decisions/0007-provenance-sourcelink-and-symbols.md))
  are published to [nuget.org](https://www.nuget.org). Only protected `main` / `release/*` / `v*` refs publish.
- Release packages should be signed and RFC-3161 timestamped so signatures outlive the certificate; verify with
  `dotnet nuget verify`. Prereleases from feature branches may publish unsigned.

## Definition of Done

The [`build/dod.ps1`](build/dod.ps1) script is the contract. It enforces:

- **CSharpier** formatting (`dotnet csharpier check .`).
- Restore + build on **net10**, with warnings-as-errors and vulnerability advisories as errors in CI.
- **Banned APIs** (`BannedApiAnalyzers`, `RS0030`) from `BannedSymbols.txt` — no `Newtonsoft.Json`, no sync-over-async —
  and from `BannedSymbols.Encoding.txt` in every project, tests included — no lenient text encodings (ADR-0266).
- **Public API surface guard** (`PublicApiAnalyzers`) — the declared surface in each packable project's
  `PublicAPI.{Shipped,Unshipped}.txt`.
- Tests with coverage — the full suite in the full gate; a pull request into `main` runs the affected tests (ADR-0263).
- Deterministic, source-linked builds.
