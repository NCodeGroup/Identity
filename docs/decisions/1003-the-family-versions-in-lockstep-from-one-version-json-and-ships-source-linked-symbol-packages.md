# 1003. The family versions in lockstep from one version.json and ships source-linked symbol packages

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0101](project-template/0101-central-package-management.md)

## Context

[project-template ADR-0101](project-template/0101-central-package-management.md) keeps package versions in
`Directory.Packages.props` through Central Package Management and leaves how the repository's own packages are versioned
and provenanced to a decision of its own. The family is many small packages that must stay mutually consistent: a
consumer that references two of them never gets a diamond-dependency version conflict.

Consumers of a shipped library also want to step into its exact released source when debugging, and want confidence that
a package was built deterministically from a known commit. This repository is public on GitHub, so its source is freely
fetchable with no authentication, which shapes the right provenance mechanism.

## Decision

**The whole family ships one version, stamped from the root `version.json` by git height, and every package ships
deterministic, source-linked builds with a symbol package and no embedded tracked source.**

### Versioning

1. **Package versions live in one place,** [`Directory.Packages.props`](../../Directory.Packages.props), as
   project-template ADR-0101 sets out. The family also turns on `CentralPackageTransitivePinningEnabled`, so a
   vulnerable indirect dependency is forced to a patched version centrally.
2. **[Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) stamps every assembly and package in
   lockstep** from the root [`version.json`](../../version.json). The package is referenced in `Directory.Build.props`,
   and the family ships one version derived from git height.
3. **`version.json` sets `assemblyVersion` precision to `major`,** so every `x.*` build shares one binding identity:
   minor and patch upgrades are drop-in within a major, and different majors load side by side.
4. **`version.json` sets `cloudBuild.setVersionVariables` to `false`.** Nerdbank.GitVersioning appends its variables
   (`GitBuildVersion` and its siblings) to `GITHUB_ENV` from every parallel MSBuild node without a lock, which tears
   lines in that file. The runner rejects a torn line at the end of the job (`Unable to process file command 'env'
   successfully`) and fails a job whose work succeeded. Nothing reads the variables. A workflow that needs the version
   asks for it explicitly with `nbgv get-version` rather than turning the export back on.

### Provenance and symbols

Gated on `IsPackable` in [`Directory.Build.targets`](../../Directory.Build.targets):

1. **Each package references [`Microsoft.SourceLink.GitHub`](https://github.com/dotnet/sourcelink) with
   `PublishRepositoryUrl`,** so the PDB maps every tracked source line to a `raw.githubusercontent.com` URL that a
   debugger fetches with no symbol server and no authentication.
2. **Tracked source is not embedded:** there is no `EmbedAllSources`. Because the repository is public and browsable,
   embedding tracked source in the PDB would only bloat every package.
3. **`EmbedUntrackedSources` embeds only files that have no URL to point at,** such as the generated
   `ThisAssembly` and `AssemblyInfo` files of Nerdbank.GitVersioning, so step-into works for them too.
4. **`DebugType=portable`, `IncludeSymbols` and `SymbolPackageFormat=snupkg`** make each package ship a companion
   `.snupkg` for the NuGet.org symbol server. Publishing pushes the `.snupkg` beside the `.nupkg`.

`Directory.Build.props` sets `ContinuousIntegrationBuild` to `true` when `CI` is `true`, for every project, so CI output
is deterministic and path-normalized.

## Options considered

- **Independent per-package SemVer.** Rejected: the packages are co-developed and co-released, and lockstep keeps the
  consumer story simple.
- **Embedding all sources and the PDB in the main `.nupkg`.** Rejected: it suits a private, authentication-gated source
  host, where a SourceLink fetch would hit an authenticated endpoint and fail. This repository is public, so embedding
  bloats every package for no gain.
- **No symbols and no SourceLink.** Rejected: it loses step-into debugging and provenance.
- **Lockstep versioning with SourceLink.GitHub and a snupkg (chosen).** The primary package stays small, the NuGet.org
  symbol-server story is the standard one, and step-into debugging reads straight from public GitHub.

## Consequences

- The version is a property of the commit, not of an edited file, so releases are reproducible from git.
- A package with no source change still gets each family release's version; that is the trade for lockstep simplicity.
- A build on GitHub Actions does not export the Nerdbank.GitVersioning variables through `GITHUB_ENV`, so no job fails
  on a torn line there.
- Consumers debug into the exact tagged source with no setup.
- The embedded-source rejection rests on the repository being public.

## References

- [`Directory.Packages.props`](../../Directory.Packages.props), [`Directory.Build.props`](../../Directory.Build.props),
  [`Directory.Build.targets`](../../Directory.Build.targets), [`version.json`](../../version.json).
- [project-template ADR-0101](project-template/0101-central-package-management.md)
