# Agent guide — NCode.Identity

An open-source **OpenID Connect + OAuth 2.0** library family and reference server for .NET (net10). It produces a set
of independently-published NuGet packages — JOSE/JWT, cryptographic secrets, the OpenID protocol model, persistence
abstractions, and DI composition — plus a runnable `Playground` reference host.

## Layout

Production projects live under `src/`; test projects (the `*Tests` projects) live under `tests/`.

- **JOSE / JWT** — `NCode.Identity.Jose(.Abstractions)`, `NCode.Identity.JsonWebTokens`
- **Secrets** — `NCode.Identity.Secrets(.Abstractions)`, `NCode.Identity.Secrets.Persistence(.Abstractions)`
- **OpenID** — `NCode.Identity.OpenId.Abstractions` + `.Core` (environments, errors, messages, serialization),
  `.Authentication(.Abstractions)` (endpoints, grant handlers, token/claims services), `.Management(.Abstractions)`
  (the `.Abstractions` package holds the wire contracts under the `NCode.Identity.OpenId.Management.Contracts` namespace),
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

## Known pending work

[`BACKLOG.md`](BACKLOG.md) is the single source of truth for documented pending work — deferred decisions, feature
gaps, and follow-ups. Record pending/deferred items **there** (and a non-obvious decision in an ADR), not in chat
history or volatile agent memory ([ADR-0008](docs/adr/0008-conventions-and-lessons-live-in-the-repository.md)).

## Build, test & contributing

Run [`./build/dod.ps1`](build/dod.ps1) — the single Definition of Done (tool-restore, CSharpier check, restore,
build + pack, test + coverage) that CI and developers both run. It fails fast on the first broken gate and prints
`Definition of Done: PASSED` as its final line on success — that line (not any generated file) is the pass signal.
Package versions are centrally managed in
[`Directory.Packages.props`](Directory.Packages.props); versioning is stamped in lockstep by Nerdbank.GitVersioning
from the root [`version.json`](version.json). See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the full workflow and
[`GOVERNANCE.md`](GOVERNANCE.md) for ownership, versioning, and publishing. For a full conventions sweep, run the
[`/audit-conventions`](.github/prompts/audit-conventions.prompt.md) prompt.

This family is **greenfield and pre-release**: all work lands on `dev` until it is feature-complete, breaking changes
are acceptable there (prefer a clean cutover over compatibility shims, and expect `PublicAPI.Unshipped.txt` churn), and
the design follows an **Auth0-parity-plus** philosophy — familiar Auth0 shapes with more flexible, data-driven controls.
See [ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md). Because of this posture, **default to the
best long-term, cohesive design**: when a change forks between a minimal compatibility-preserving patch and the
comprehensive, correct model, choose the comprehensive model **without** weighing refactor difficulty or public-surface
breakage, and surface a fork for the user only when the options are genuinely equal in quality. Full statement: the
standing posture in [`BACKLOG.md`](BACKLOG.md).
