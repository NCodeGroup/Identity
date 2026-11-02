# 1003. Central Package Management and lockstep versioning

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0101](project-template/0101-central-package-management.md)

## Context

[project-template ADR-0101](project-template/0101-central-package-management.md) keeps package versions in
`Directory.Packages.props` through Central Package Management and leaves how the repository's own packages are versioned
to a decision of its own. The family is many small packages that must stay mutually consistent: a consumer that
references two of them never gets a diamond-dependency version conflict.

## Decision

- **Package versions live in one place**, [`Directory.Packages.props`](../../Directory.Packages.props), as
  project-template ADR-0101 sets out, with transitive pinning enabled (`CentralPackageTransitivePinningEnabled`) so a
  vulnerable indirect dependency is forced to a patched version centrally.
- **Assembly versions are stamped in lockstep by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)**
  from a single root [`version.json`](../../version.json). The whole family ships one version derived from git height,
  and `AssemblyVersion` is pinned to `major` precision so every `x.*` build shares one binding identity — patch and
  minor upgrades are drop-in within a major, and different majors load side by side.

## Options considered

- **Independent per-package SemVer.** Rejected for now: the packages are co-developed and co-released; lockstep keeps
  the consumer story simple. Independent versioning can be revisited if a package's cadence genuinely diverges.

## Consequences

- The version is a property of the commit, not of an edited file — releases are reproducible from git.
- A package with no source change still gets a new version on each family release; that is the intended trade for
  lockstep simplicity.
- The version is stamped into the assemblies and packages only; `version.json` sets `cloudBuild.setVersionVariables` to
  `false`, so a build on GitHub Actions does not export `GitBuildVersion` and its siblings through `GITHUB_ENV`. Nothing
  reads them, and Nerdbank.GitVersioning 3.10 appends them from every parallel MSBuild node without a lock, which tears
  lines in that file. The runner rejects a torn line at the end of the job (`Unable to process file command 'env'
  successfully`), failing a job whose work succeeded. A workflow that later needs the version asks for it explicitly
  (`nbgv get-version`) rather than turning the export back on.

## References

- [`Directory.Packages.props`](../../Directory.Packages.props), [`Directory.Build.props`](../../Directory.Build.props),
  [`version.json`](../../version.json).
