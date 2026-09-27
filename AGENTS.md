# Agent guide — NCode.Identity

An open-source **OpenID Connect + OAuth 2.0** library family and reference server for .NET (net10). It produces a set
of independently-published NuGet packages — JOSE/JWT, cryptographic secrets, the OpenID protocol model, persistence
abstractions, and DI composition — plus a runnable `Playground` reference host.

## Layout

Projects live at the repository **root** (there is no `src/` folder); test projects are the `*Tests` projects.

- **JOSE / JWT** — `NCode.Identity.Jose(.Abstractions)`, `NCode.Identity.JsonWebTokens`
- **Secrets** — `NCode.Identity.Secrets(.Abstractions)`, `NCode.Identity.Secrets.Persistence(.Abstractions)`
- **OpenID** — `NCode.Identity.OpenId.Abstractions` + `.Core` (environments, errors, messages, serialization),
  `.Authentication(.Abstractions)` (endpoints, grant handlers, token/claims services), `.Management`,
  `.Messages.Abstractions`, `.Persistence(.Abstractions)`, `.Persistence.EntityFramework`
- **Persistence** — `NCode.Persistence.Abstractions`, `NCode.Identity.Persistence.Abstractions`
- **Composition / host** — `NCode.Registration` (the `IServiceBuilder<TMarker>` builder), `NCode.Identity`,
  `NCode.Identity.Server` (`AddIdentityServer()`), `NCode.Identity.OpenId.Playground` (ASP.NET Core reference host)

The central extensibility model — **service** vs **strategy collection** vs **mediator** — is captured in
[ADR-0001](docs/adr/0001-mediator-vs-dependency-injection-logic-classes.md).

## Coding conventions

The coding-convention instruction files are the single source of truth, auto-applied by scope:
[`csharp-production.instructions.md`](.github/instructions/csharp-production.instructions.md) and
[`csharp-testing.instructions.md`](.github/instructions/csharp-testing.instructions.md). The _reasoning_ behind
non-obvious decisions lives in [`docs/adr/`](docs/adr/).

## Build, test & contributing

Run [`./build/dod.ps1`](build/dod.ps1) — the single Definition of Done (tool-restore, CSharpier check, restore,
build + pack, test + coverage) that CI and developers both run. Package versions are centrally managed in
[`Directory.Packages.props`](Directory.Packages.props); versioning is stamped in lockstep by Nerdbank.GitVersioning
from the root [`version.json`](version.json). See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the full workflow and
[`GOVERNANCE.md`](GOVERNANCE.md) for ownership, versioning, and publishing. For a full conventions sweep, run the
[`/audit-conventions`](.github/prompts/audit-conventions.prompt.md) prompt.
