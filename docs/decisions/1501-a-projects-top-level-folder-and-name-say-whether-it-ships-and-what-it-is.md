# 1501. A project's top-level folder and name say whether it ships and what it is

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** NCode Group

## Context

A person or an agent reads a project's role from its folder and its name. When published packages, deployables, a build
analyzer and a pnpm workspace share one folder, the folder hides whether a project ships, and only each project's own
`IsPackable` setting tells. A reader has to open a project file to learn whether a change reaches a consumer
([#759](https://github.com/NCodeGroup/identity/issues/759)).

Names carry the same signal. `NCode.Identity.Server`, the deployable host and the local-dev orchestrator can read as
synonyms, although one is a library that ships, one is a process that is deployed and one is a developer-only tool.
`AppHost` is Aspire's own jargon and sounds like the host of the application, while Aspire is local-development only
([ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md)). A name such as "playground" on something internet-facing invites the
reasoning that leaves ephemeral signing keys, a wildcard `AllowedHosts` and development seeding switched on.

## Decision

**A project's top-level folder says whether it ships: `src/` holds only the published NuGet packages, every other role
has a folder of its own, and the build fails a project whose folder and packing disagree. Its name says what it is.**
`AGENTS.md` lists this as overriding the project template's instruction that production projects live under `src/`.

1. **`src/` holds only the published NuGet packages.** Every project there packs.
2. **`hosts/` holds the deployables:** `NCode.Identity.OpenId.Host`, `NCode.Identity.OpenId.Host.Database`,
   `NCode.Identity.OpenId.Host.MigrationService`, `NCode.Identity.OpenId.LocalDev` and
   `NCode.Identity.ManagementConsole.Host`. `hosts/Directory.Build.props` turns packing off.
3. **`analyzers/` holds `NCode.Identity.Analyzers`,** which every project's build loads and no package ships.
   `analyzers/Directory.Build.props` turns packing off.
4. **`frontend/` at the repository root holds the pnpm workspace,** which is not a NuGet package.
5. **`tests/` and `benchmarks/` hold their projects and turn packing off in their own `Directory.Build.props`.** No
   project outside `src/` sets `IsPackable` itself. `charts/`, `brand/`, `build/` and `docs/` keep their roles.
6. **The `RequirePackingFollowsFolder` target in `Directory.Build.targets` fails a project under `src/` that does not
   pack and a project outside `src/` that does,** in every build: the IDE, `dod.ps1`, CI and the image builds. The
   message names the folder rule.
7. **Tools follow the folders.** `build/affected-tests.ps1` maps `src/`, `hosts/`, `analyzers/` and `tests/`;
   `build/doc-comment-citations.ps1` scans the same four; the production C# conventions apply to `src/`, `hosts/` and
   `analyzers/`; and `.dockerignore` admits `src/`, `hosts/`, `analyzers/` and `frontend/`, the four project folders of
   the image builds' context.
8. **Names distinguish role.** `NCode.Identity.Server` is the shipping library whose entry point is
   `AddIdentityServer()`. `…Host` is the deployable, and the `Host.` prefix groups the projects that travel with it: its
   EF migrations (`.Database`, host-owned, per [ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md))
   and its migration worker (`.MigrationService`).
9. **The Aspire local-dev orchestrator is `NCode.Identity.OpenId.LocalDev`,** named for its purpose because it is never
   deployed. Aspire finds it through `<IsAspireHost>true</IsAspireHost>` and `Aspire.AppHost.Sdk`, not by name. "AppHost"
   appears only as Aspire's vocabulary, such as in `Aspire.Hosting.AppHost`.
10. **A deployable's name never suggests a toy.** A "playground" name invites unsafe defaults in something
    internet-facing.
11. **The frontend flow explorer `apps/playground` (`@ncode/app-playground`) keeps its name,** with its `playground-spa`
    client and `playground` bootstrap surface, because it is a genuine playground
    ([ADR-1082](1082-playground-and-conformance-surfaces.md)).

## Options considered

- **`IsPackable` flags alone, with every project under `src/`.** Rejected: the folder says nothing about whether a
  project ships, and nothing checks that a flag matches a project's role.
- **The analyzer under `build/`.** Rejected: `build/` holds scripts and the project template's build files, and
  `build/affected-tests.ps1` treats every change there as global, so an analyzer edit would run every test.
- **The pnpm workspace under `src/`.** Rejected: it is not a package, and `src/` holds only packages.
- **`AppHost` as the orchestrator's name.** Rejected: it sounds central, when the orchestrator is a developer-only tool.
- **`Aspire` as the orchestrator's name.** Rejected: it names the current technology, while `LocalDev` stays true if the
  orchestrator is rebuilt on something else.
- **Folder and name that state the role (chosen).** The folder tells whether a project ships, the build checks it, and
  the name tells what the project is.

## Consequences

- A new deployable goes under `hosts/` and packs nothing without a flag; a new package goes under `src/` and packs
  without one.
- A project in the wrong folder fails the build with a message that names the folder rule.
- The image builds' context holds four project folders: `src/`, `hosts/`, `analyzers/` and `frontend/`.
- Files under `frontend/` link to the repository with one `../` above the workspace.
- The split between the library a consumer references and the process an operator runs is visible in the names.

## References

- Issue [#759](https://github.com/NCodeGroup/identity/issues/759).
- [ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md),
  [ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md),
  [ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md),
  [ADR-1082](1082-playground-and-conformance-surfaces.md),
  [ADR-1386](1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md),
  [ADR-1430](1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md),
  [ADR-1486](1486-the-repositorys-own-analyzer-project-fails-the-build-on-a-field-an-auto-property-could-replace.md)
- `Directory.Build.targets`, `hosts/Directory.Build.props`
