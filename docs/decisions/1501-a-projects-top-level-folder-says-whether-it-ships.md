# 1501. A project's top-level folder says whether it ships

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** NCode Group
- **Amends:** [ADR-1064](1064-frontend-presentation-platform.md) (placement),
  [ADR-1486](1486-an-analyzer-in-every-project-reports-a-field-that-could-be-an-auto-property.md) (decision 1)

## Context

A person or an agent reads a project's role from its folder. When published packages, deployables, a build analyzer and
a pnpm workspace share one folder, the folder hides whether a project ships, and only each project's own `IsPackable`
setting tells. A reader has to open a project file to learn whether a change reaches a consumer
([#759](https://github.com/NCodeGroup/Identity/issues/759)).

## Decision

**A project's top-level folder says whether it ships: `src/` holds only the published NuGet packages, every other role
has a folder of its own, and the build fails a project whose folder and packing disagree.**

1. **`src/` holds only the published NuGet packages.** Every project there packs.
2. **`hosts/` holds the deployables:** `NCode.Identity.OpenId.Host`, `NCode.Identity.OpenId.Host.Database`,
   `NCode.Identity.OpenId.Host.MigrationService`, `NCode.Identity.OpenId.LocalDev` and
   `NCode.Identity.ManagementConsole.Host`. `hosts/Directory.Build.props` turns packing off.
3. **`analyzers/` holds `NCode.Identity.Analyzers`,** which every project's build loads and no package ships.
   `analyzers/Directory.Build.props` turns packing off.
4. **`frontend/` at the repository root holds the pnpm workspace,** which is not a NuGet package.
5. **`tests/` and `benchmarks/` turn packing off in their own `Directory.Build.props`,** and no project outside `src/`
   sets `IsPackable` itself. `charts/`, `brand/`, `build/` and `docs/` keep their roles.
6. **The `RequirePackingFollowsFolder` target in `Directory.Build.targets` fails a project under `src/` that does not
   pack and a project outside `src/` that does,** in every build: the IDE, `dod.ps1`, CI and the image builds.
7. **Tools follow the folders.** `build/affected-tests.ps1` maps `src/`, `hosts/`, `analyzers/` and `tests/`;
   `build/doc-comment-citations.ps1` scans the same four; the production C# conventions apply to `src/`, `hosts/` and
   `analyzers/`; and `.dockerignore` admits `src/`, `hosts/`, `analyzers/` and `frontend/`.

## Options considered

- **`IsPackable` flags alone, with every project under `src/`.** Rejected: the folder says nothing about whether a
  project ships, and nothing checks that a flag matches a project's role.
- **The analyzer under `build/`.** Rejected: `build/` holds scripts and the project template's build files, and
  `build/affected-tests.ps1` treats every change there as global, so an analyzer edit would run every test.
- **The pnpm workspace under `src/`.** Rejected: it is not a package, and `src/` holds only packages.

## Consequences

- A new deployable goes under `hosts/` and packs nothing without a flag; a new package goes under `src/` and packs
  without one.
- The image builds' context holds four project folders: `src/`, `hosts/`, `analyzers/` and `frontend/`.
- Files under `frontend/` link to the repository with one `../` above the workspace.
- A project in the wrong folder fails the build with a message that names the folder rule.

## References

- Issue [#759](https://github.com/NCodeGroup/Identity/issues/759).
- [ADR-1064](1064-frontend-presentation-platform.md),
  [ADR-1386](1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md),
  [ADR-1430](1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md),
  [ADR-1486](1486-an-analyzer-in-every-project-reports-a-field-that-could-be-an-auto-property.md)
- `Directory.Build.targets`, `hosts/Directory.Build.props`
