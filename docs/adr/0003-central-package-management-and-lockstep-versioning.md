# 3. Central Package Management and lockstep versioning

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group

## Context

The family is many small packages that must stay mutually consistent — a consumer that references two of them should
never get a diamond-dependency version conflict, and a maintainer should never have to bump a dependency version in a
dozen `.csproj` files by hand. Two separate concerns were previously entangled in each project file: **which** version
of a package to use, and **which** projects use it.

## Decision

- **Package versions live in one place.** [`Directory.Packages.props`](../../Directory.Packages.props) enables NuGet
  [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management)
  (`ManagePackageVersionsCentrally=true`). Each `<PackageVersion>` names the version once; a project's
  `<PackageReference>` carries **no** `Version` attribute (NuGet errors with `NU1008` if it does). Transitive pinning
  is enabled so a vulnerable indirect dependency can be forced to a patched version centrally.
- **Assembly versions are stamped in lockstep by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)**
  from a single root [`version.json`](../../version.json). The whole family ships one version derived from git height,
  and `AssemblyVersion` is pinned to `major` precision so every `x.*` build shares one binding identity — patch and
  minor upgrades are drop-in within a major, and different majors load side by side.

## Options considered

- **Per-project inline versions (the prior state).** Rejected: version drift across projects, and a dependency bump is
  an N-file edit that is easy to do incompletely.
- **A shared `.props` with `$(SomeVersion)` properties.** Works, but reinvents a worse CPM; CPM is the first-class
  NuGet mechanism with tooling and transitive pinning.
- **Independent per-package SemVer.** Rejected for now: the packages are co-developed and co-released; lockstep keeps
  the consumer story simple. Independent versioning can be revisited if a package's cadence genuinely diverges.

## Consequences

- Adding/upgrading a dependency is a one-line change in `Directory.Packages.props`; every consumer moves together.
- The version is a property of the commit, not of an edited file — releases are reproducible from git.
- A package with no source change still gets a new version on each family release; that is the intended trade for
  lockstep simplicity.

## References

- [`Directory.Packages.props`](../../Directory.Packages.props), [`Directory.Build.props`](../../Directory.Build.props),
  [`version.json`](../../version.json).
