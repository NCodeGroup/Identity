# Agent guide — NCode.Identity

An open-source **OpenID Connect + OAuth 2.0** library family and reference server for .NET (net10). It produces a set
of independently-published NuGet packages — JOSE/JWT, cryptographic secrets, the OpenID protocol model, persistence
abstractions, and DI composition — plus a runnable `NCode.Identity.OpenId.Host` reference host.

## Layout

Production projects live under `src/`; test projects (the `*Tests` projects) live under `tests/`.

- **JOSE / JWT** — `NCode.Identity.Jose(.Abstractions)`, `NCode.Identity.JsonWebTokens`
- **Secrets** — `NCode.Identity.Secrets(.Abstractions)`, `NCode.Identity.Secrets.Persistence(.Abstractions)`
- **OpenID** — `NCode.Identity.OpenId.Abstractions` + `.Core` (environments, errors, messages, serialization),
  `.Authentication(.Abstractions)` (endpoints, grant handlers, token/claims services), `.Management(.Abstractions)`
  (the `.Abstractions` package holds the wire contracts under the `NCode.Identity.OpenId.Management.Contracts` namespace),
  `.Messages.Abstractions`, `.Persistence(.Abstractions)`, `.Persistence.EntityFramework`
- **Persistence** — `NCode.Persistence.Abstractions`, `NCode.Identity.Persistence.Abstractions`
- **Infrastructure ports** — `NCode.Identity.Dispatch(.Abstractions)` (background jobs),
  `NCode.Identity.Events(.Abstractions)`, `NCode.Identity.SharedState(.Abstractions)` (state every replica sees) and its
  opt-in `NCode.Identity.SharedState.Valkey` adapter
  ([ADR-0164](docs/adr/0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md))
- **Composition / host** — `NCode.Registration` (the `IServiceBuilder<TMarker>` builder), `NCode.Identity`,
  `NCode.Identity.Server` (`AddIdentityServer()`), `NCode.Identity.OpenId.Host` (ASP.NET Core reference host),
  `NCode.Identity.OpenId.LocalDev` (.NET Aspire local-dev orchestrator — [ADR-0071](docs/adr/0071-aspire-local-dev-orchestrator.md))

The central extensibility model — **service** vs **strategy collection** vs **mediator** — is captured in
[ADR-0001](docs/adr/0001-mediator-vs-dependency-injection-logic-classes.md). Extensibility is the family's goal: a
consumer or sibling package extends any vocabulary (settings, setting scopes, profiles, grant types, methods) by
registering into it, so vocabularies are **open by default** and a closed construct such as an `enum` must be provably
closed ([ADR-0150](docs/adr/0150-vocabularies-are-open-unless-provably-closed.md)).

## Coding conventions

The coding-convention instruction files are the single source of truth, auto-applied by scope:
[`csharp-production.instructions.md`](.github/instructions/csharp-production.instructions.md) and
[`csharp-testing.instructions.md`](.github/instructions/csharp-testing.instructions.md). The _reasoning_ behind
non-obvious decisions lives in [`docs/adr/`](docs/adr/).

## Known pending work

[`BACKLOG.md`](BACKLOG.md) is the single source of truth for documented pending work — deferred decisions, feature
gaps, and follow-ups. Record pending/deferred items **there** (and a non-obvious decision in an ADR), not in chat
history or volatile agent memory ([ADR-0008](docs/adr/0008-conventions-and-lessons-live-in-the-repository.md)). When an
item is fully done, **move** its text to the completed-items archive [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md)
(don't just delete it) so completed work stays discoverable without git archaeology.

## Plan-and-execute workflow

Multi-step changes run as **plan-and-execute**
([ADR-0170](docs/adr/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md)):
Claude Opus plans into a root `PLAN.md`, a lower-cost Copilot model (Auto) executes it one verified step at a time, and
any step that needs a decision comes back to Opus to be re-planned.

- **VS Code** (session target Local): pick the **Copilot Planner** agent, or run a planning prompt — `/plan-feature`,
  `/plan-bugfix`, `/plan-refactor`, `/plan-tests`, `/plan-migration`, `/plan-performance`, `/plan-security`,
  `/plan-docs`, or `/handoff-plan` after planning in conversation. Review `PLAN.md`, then either **Run all steps with
  subagents** (the Copilot Orchestrator on Opus delegates each step to a Copilot Implementer subagent on Auto, runs the
  step's check, and re-plans escalations itself — enable the Run Subagent tool) or step through with **Execute next
  step** and **Re-plan with the planner**. Finish with **Review the work** (Sonnet) and execute the fix steps it queues.
- **Copilot CLI:** start on Opus — `copilot --model claude-opus-5.5 --reasoning-effort high` — then `/plan-feature …`,
  `/orchestrate-plan` (delegates each step to the `plan-executor` agent on `auto`), and `/review-plan-diff`.
- **Done-when commands** prove one step: the narrowest test that fails until the step is done, such as
  `dotnet test tests/<Project>/<Project>.csproj --filter "FullyQualifiedName~<TestClass>"` (xUnit v2 on VSTest).
  `./build/dod.ps1 -Fast` stays the gate for the whole change.
- **`PLAN.md` is transient:** the push protocol in [`copilot-instructions.md`](.github/copilot-instructions.md) promotes
  its Decisions, deletes it, and squashes the `Plan step N` / `Re-plan step N` checkpoint commits before anything is
  pushed.
- The agents, prompts, and skills under `.github/` are **generated** (the installer command is in ADR-0170); change the
  templates and re-install rather than hand-editing them. Repository rules belong in the instruction files, which every
  agent in the loop already loads.

## Build, test & contributing

Run [`./build/dod.ps1`](build/dod.ps1) — the single Definition of Done (tool-restore, CSharpier check, restore,
build + pack, test + coverage) that CI and developers both run. It fails fast on the first broken gate and prints
`Definition of Done: PASSED` as its final line on success — that line (not any generated file) is the pass signal.
For local development — including any protocol or validation step that runs the Definition of Done — use
`./build/dod.ps1 -Fast`: it parallelizes project builds and test assemblies and skips the non-correctness stages
(coverage + packing) while keeping compile warnings-as-errors and analyzers. `./build/dod.ps1 -Fast` is the **local
pre-push gate** (the push protocol's final check); the plain `./build/dod.ps1` is the CI-parity gate that CI runs (also
available locally for a thorough clean Release check). Package versions are centrally managed in
[`Directory.Packages.props`](Directory.Packages.props); versioning is stamped in lockstep by Nerdbank.GitVersioning
from the root [`version.json`](version.json). See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the full workflow and
[`GOVERNANCE.md`](GOVERNANCE.md) for ownership, versioning, and publishing. For a full conventions sweep, run the
[`/audit-conventions`](.github/prompts/audit-conventions.prompt.md) prompt.

This family is **greenfield and pre-release**: all work lands on the single `main` trunk until it is feature-complete
([ADR-0125](docs/adr/0125-trunk-based-development-on-main.md)), breaking changes
are acceptable (prefer a clean cutover over compatibility shims, and expect `PublicAPI.Unshipped.txt` churn), and
the design follows an **Auth0-parity-plus** philosophy — familiar Auth0 shapes with more flexible, data-driven controls.
See [ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md). Because of this posture, **default to the
best long-term, cohesive design**: when a change forks between a minimal compatibility-preserving patch and the
comprehensive, correct model, choose the comprehensive model **without** weighing refactor difficulty or public-surface
breakage, and surface a fork for the user only when the options are genuinely equal in quality. Full statement: the
standing posture in [`BACKLOG.md`](BACKLOG.md).
