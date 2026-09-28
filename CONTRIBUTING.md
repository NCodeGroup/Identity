# Contributing to NCode.Identity

Thanks for helping improve NCode.Identity. These are shipped, security-relevant libraries, so they hold to an explicit,
enforced quality bar.

## Prerequisites

- The .NET SDK pinned in [`global.json`](global.json) (10.0.x). The net10 targeting pack restores automatically.
- PowerShell 7+ (`pwsh`) to run the Definition-of-Done script.
- Local tools: run `dotnet tool restore` once after cloning to install the repo-pinned **CSharpier** formatter.

## Build and test

```pwsh
# One-time after cloning — installs pinned local tools (CSharpier).
dotnet tool restore

# From the repo root — the single Definition-of-Done entrypoint CI also runs
# (CSharpier check + restore + build + pack + tests + coverage):
./build/dod.ps1

# Or drive the tools directly:
dotnet csharpier format .    # format before committing
dotnet build NCode.Identity.slnx
dotnet test NCode.Identity.slnx
```

## Coding standards

**The coding-convention instruction files are the authoritative checklist** — auto-applied by scope
([`csharp-production.instructions.md`](.github/instructions/csharp-production.instructions.md) and
[`csharp-testing.instructions.md`](.github/instructions/csharp-testing.instructions.md)); every rule lives there once,
marked tool-enforced or review-only. The highlights:

- `ValueTask` / `ValueTask<T>` for async; `Async` suffix; `CancellationToken` last.
- Nullable enabled; restructure so the compiler proves non-null instead of using `!`.
- `sealed` / `init` / concrete collections; enums only for closed vocabularies (extensible ones are `const string`).
- No behavior-selecting flag arguments; split into intention-revealing methods instead.
- **No cross-assembly `InternalsVisibleTo`** in production — packages compose through public contracts
  ([ADR-0004](docs/adr/0004-no-cross-assembly-internalsvisibleto.md)).
- One type per file; the single canonical implementation of `IFoo` is `DefaultFoo`
  ([ADR-0006](docs/adr/0006-single-canonical-impl-is-defaultfoo.md)); C# 14 `extension` syntax.

## Making a change

1. Open (or find) a GitHub issue describing the problem or proposal. A change that affects the **public outcome**
   (semantics, public surface, validation/security posture) should reach agreement on the issue — and be recorded as an
   ADR under [`docs/adr/`](docs/adr/) — before the pull request.
2. Branch from `dev`. Keep the public surface deliberate; a breaking change to the public API is a **major** version bump.
3. Record any consumer-visible change in [`CHANGELOG.md`](CHANGELOG.md) under `## [Unreleased]`.
4. Ensure `./build/dod.ps1` passes locally before opening a pull request.
5. Open a PR; a maintainer reviews and approves. `main` is protected — no direct pushes, no self-approval.

See [GOVERNANCE.md](GOVERNANCE.md) for ownership, the release lifecycle, and how versions map onto tags.

## Code formatting — CSharpier

Formatting is owned by [CSharpier](https://csharpier.com), pinned as a **local tool** in
[`.config/dotnet-tools.json`](.config/dotnet-tools.json) and enforced by the Definition of Done
(`dotnet csharpier check .`), so formatting is a build gate, not a review debate.

```shell
dotnet tool restore         # once, after cloning — installs the repo-pinned CSharpier
dotnet csharpier format .   # format everything
dotnet csharpier check .    # verify formatting (what CI runs; fails if anything is unformatted)
```

The version is pinned with `rollForward: false` so formatting output never drifts between CSharpier versions.

## Package versions — Central Package Management (CPM)

Package **versions live in one place** ([`Directory.Packages.props`](Directory.Packages.props)) and are not repeated
per project ([ADR-0003](docs/adr/0003-central-package-management-and-lockstep-versioning.md)).

- A `<PackageVersion Include="X" Version="1.2.3" />` in `Directory.Packages.props` says **which version**.
- A `<PackageReference Include="X" />` (no `Version`) in a `.csproj` says **which project uses it**. NuGet errors with
  `NU1008` if a `PackageReference` carries an inline version while CPM is on.
- References every project needs (Nerdbank.GitVersioning) sit as versionless `PackageReference`s in
  [`Directory.Build.props`](Directory.Build.props); provenance/analyzer references sit in
  [`Directory.Build.targets`](Directory.Build.targets), gated on `IsPackable`.

To upgrade a package, change its single `Version` in `Directory.Packages.props` — every consuming project moves together.

## Public API surface guard (`PublicAPI.*.txt`)

Because these are shipped libraries, an accidental change to the public API surface is a break for every consumer. The
[`Microsoft.CodeAnalysis.PublicApiAnalyzers`](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
Roslyn analyzer makes the surface explicit and reviewable via a `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`
pair per packable project ([ADR-0005](docs/adr/0005-public-api-surface-is-tracked-and-evolves-compatibly.md)):

- Add public API → add its lines to `PublicAPI.Unshipped.txt` (the IDE's _"Add to public API"_ code fix generates them).
- Remove public API → prefix the removed line with `*REMOVED*` in `PublicAPI.Unshipped.txt`.
- At release → move everything from `Unshipped` into `Shipped`. Pre-1.0, the whole surface stays in `Unshipped`.

Reviewing a PR then includes reviewing the diff of these files. See
[`docs/api-compatibility.md`](docs/api-compatibility.md) for the full backward-compatibility procedure.
