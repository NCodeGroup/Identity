# 7. Provenance — SourceLink to GitHub and a portable symbol package

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group

## Context

Consumers of a shipped library want to step into its exact released source when debugging, and want confidence that a
package was built deterministically from a known commit. This repository is **public on GitHub**, so the source is
freely fetchable with no authentication — which shapes the right provenance mechanism.

## Decision

**Ship deterministic, source-linked builds with a portable symbol package — and do _not_ embed source.** Because the
repository is public and browsable, tracked source is served from `raw.githubusercontent.com`; embedding it in the PDB
would only bloat every package. Gated on `IsPackable` in [`Directory.Build.targets`](../../Directory.Build.targets):

- [`Microsoft.SourceLink.GitHub`](https://github.com/dotnet/sourcelink) plus `PublishRepositoryUrl`, so the PDB maps
  every **tracked** source line to a `raw.githubusercontent.com` URL a debugger fetches with no symbol server and no
  auth wall. Tracked `.cs` is **not** embedded (no `EmbedAllSources`).
- `EmbedUntrackedSources` embeds **only** files that have no URL to point at — generated files such as the
  Nerdbank.GitVersioning `ThisAssembly`/`AssemblyInfo` — so step-into still works for them.
- `DebugType=portable` with `IncludeSymbols` + `SymbolPackageFormat=snupkg`, so each package ships a companion
  `.snupkg` that can be published to the NuGet.org symbol server.
- `ContinuousIntegrationBuild=true` on CI for deterministic, path-normalized output.

Because the repository is public, this is preferable to **embedding all sources inside the main `.nupkg`**: SourceLink
keeps the primary package small, and there is no authenticated fetch to fail (the trade-off that would push a
_private_/auth-gated repository toward embedding sources instead).

## Options considered

- **Embed all sources + PDB inside the main `.nupkg`.** The right choice for a private, auth-gated source host (a
  SourceLink fetch would hit an authenticated endpoint and fail). Rejected here: this repo is public, so it needlessly
  bloats every package.
- **No symbols / no SourceLink.** Rejected: no step-into debugging, no provenance.
- **SourceLink.GitHub + snupkg (chosen).** Small primary package, standard NuGet.org symbol-server story, and
  step-into debugging straight from public GitHub.

## Consequences

- Consumers debug into the exact tagged source with zero setup.
- Publishing includes pushing the `.snupkg` alongside the `.nupkg`.
- If the repository ever becomes private, revisit this in favor of embedded sources (SourceLink would then require auth).

## References

- [`Directory.Build.targets`](../../Directory.Build.targets), [`Directory.Packages.props`](../../Directory.Packages.props).
