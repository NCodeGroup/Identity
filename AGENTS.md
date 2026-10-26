# Agent guide — NCode.Identity

An open-source **OpenID Connect + OAuth 2.0** library family and reference server for .NET (net10). It produces a set
of independently-published NuGet packages — JOSE/JWT, cryptographic secrets, the OpenID protocol model, persistence
abstractions, and DI composition — plus a runnable `NCode.Identity.OpenId.Host` reference host.

## Project Template

- @.github/instructions/project-template.instructions.md — the plan-then-execute workflow and base conventions, from the project template. Template updates replace it; never edit it. Where this file or a file it lists says otherwise, the project's instruction wins.

## Layout

Production projects live under `src/`; test projects (the `*Tests` projects) live under `tests/`.

- **JOSE / JWT** — `NCode.Identity.Jose(.Abstractions)`, `NCode.Identity.JsonWebTokens`
- **Secrets** — `NCode.Identity.Secrets(.Abstractions)`, `NCode.Identity.Secrets.Persistence(.Abstractions)` and its
  opt-in root key provider adapters `NCode.Identity.Secrets.Persistence.AwsKms`, `.AzureKeyVault` and `.GoogleCloudKms`
  ([ADR-0235](docs/adr/0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-0260](docs/adr/0260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md))
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

The repository-wide agent rules — design posture, orientation, commit attribution and the push protocol — are in:

- @.github/instructions/repository.instructions.md — Copilot applies it to every file, and Claude Code loads it through this line.

The project template's generic [`csharp.instructions.md`](.github/instructions/csharp.instructions.md) also applies to
every `.cs` file; where it and `csharp-production.instructions.md` or `csharp-testing.instructions.md` differ, those two
files win. Claude Code ignores `applyTo`, so [`.claude/rules/csharp-production.md`](.claude/rules/csharp-production.md)
and [`.claude/rules/csharp-testing.md`](.claude/rules/csharp-testing.md) point it at them by path.

## Known pending work

[`BACKLOG.md`](BACKLOG.md) is the single source of truth for documented pending work — deferred decisions, feature
gaps, and follow-ups. Record pending/deferred items **there** (and a non-obvious decision in an ADR), not in chat
history or volatile agent memory ([ADR-0008](docs/adr/0008-conventions-and-lessons-live-in-the-repository.md)). When an
item is fully done, **move** its text to the completed-items archive [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md)
(don't just delete it) so completed work stays discoverable without git archaeology.

## Plan-and-execute workflow

Multi-step changes run as **plan-and-execute**
([ADR-0170](docs/adr/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md)):
Claude Opus plans into a root `PLAN.md`, Claude Sonnet 5.5 executes it one verified step at a time, and any step that
needs a decision comes back to Opus to be re-planned — in VS Code, Copilot CLI or Claude Code.

- **VS Code** (session target Local): pick the **Copilot Planner** agent **and Claude Opus 5.5 in the model picker** —
  in a Local session, choosing an agent from the picker keeps the selected model (an agent file's `model` applies to it
  only as a subagent, and each handoff button selects its own). Review `PLAN.md`, then either **Run all steps with
  subagents** (the Copilot Orchestrator on Opus delegates each step to a Copilot Implementer subagent on Claude Sonnet
  5.5, runs the step's check, and re-plans escalations itself — its agent file grants the subagent tool, so nothing
  needs enabling) or step through with **Execute next step** and **Re-plan with the planner**. Finish with **Review the
  work** (Sonnet) and execute the fix steps it queues. The planning prompts — `/plan-feature`, `/plan-bugfix`,
  `/plan-refactor`, `/plan-tests`, `/plan-migration`, `/plan-performance`, `/plan-security`, `/plan-docs`, and
  `/handoff-plan` after planning in conversation — select Opus themselves but run in the built-in Agent, which has no
  handoff buttons: continue by picking **Copilot Orchestrator** with Opus and asking it to execute `PLAN.md`.
- **Copilot CLI:** start on Opus — `copilot --model claude-opus-5.5 --reasoning-effort high` — then `/plan-feature …`,
  `/orchestrate-plan` (delegates each step to the `plan-executor` agent on `claude-sonnet-5.5`), and
  `/review-plan-diff`.
- **VS Code (session target Copilot):** use the same skills as Copilot CLI. Select **Claude Opus 5.5** in the model
  picker before `/plan-feature …` or `/orchestrate-plan`; a skill runs on the session's model and cannot switch it. The
  orchestrator delegates execution as above. Select **Claude Sonnet 5.5** before `/review-plan-diff`, or the intended
  execution model before running `/execute-step` yourself. Selecting Auto for the planning session does not become Opus
  merely because the corresponding Local prompt selects it. If this session cannot delegate to the Claude Sonnet 5.5
  executor, select Claude Sonnet 5.5 yourself, run `/execute-step` one step at a time, and switch back to the planning
  model for verification or re-planning. Do not hand-edit generated agents to work around a session-specific
  model-availability error.
- **Claude Code** (CLI, VS Code extension or desktop app): run `/plan-feature …` and the other planning commands on
  Claude Opus 5.5. Each plan lives in its own git worktree, `.claude/worktrees/<name>/` on the branch
  `worktree-<name>`; `/orchestrate-plan` (from another session, `/orchestrate-plan <name>`) delegates each step to
  the `plan-executor` subagent on Claude Sonnet 5.5, and `/review-plan-diff` reviews the result.
  [`CLAUDE.md`](CLAUDE.md) imports this file.
- **Done-when commands** prove one step: the narrowest test that fails until the step is done, such as
  `dotnet test tests/<Project>/<Project>.csproj --filter "FullyQualifiedName~<TestClass>" -- RunConfiguration.TreatNoTestsAsError=true`
  (xUnit v2 on VSTest). Keep the trailing setting: without it a filter that matches no test exits `0`, so the check
  passes before its test exists. `./build/dod.ps1 -Fast -Affected` stays the gate for the whole change.
- **`PLAN.md` is transient:** it is gitignored and never committed, and the push protocol in
  [`repository.instructions.md`](.github/instructions/repository.instructions.md) promotes its Decisions, deletes it,
  and squashes the `Plan: <title>` / `Plan step N` / `Re-plan step N` checkpoint commits before anything is pushed.
- The agents, prompts and skills under `.github/`, the commands under `.claude/commands/`,
  `.claude/agents/plan-executor.md` and `CLAUDE.md` **come from the project template**
  ([`polewskm/project-template`](https://github.com/polewskm/project-template); the version is in
  [`.copier-answers.yml`](.copier-answers.yml)), which owns them: never hand-edit them. Change the workflow in
  project-template, then pull the new version in with `./.template/Update-Project.ps1`. Repository rules belong in the
  instruction files, which every agent in the loop already loads. The hand-written `audit-conventions` prompt and
  skill are the exception; edit both together.

### Local and Copilot customizations

**Both session targets are intentionally supported.** Each workflow has a Local adapter under
[`.github/prompts/`](.github/prompts/) and a Copilot adapter under [`.github/skills/`](.github/skills/).
Local supports prompt-specific agent/model/tool selection; Copilot uses skills and the session's selected model.
Both copies appearing in Local's slash menu is expected, not unfinished migration work. Claude Code reads its own
copies of the same workflows from `.claude/commands/`, which the template generates from the same sources, so the
customization check does not compare them.

The [customization check](build/agent-customizations.ps1) enforces matching command names, descriptions and ordered
workflow bodies, allowing only the argument syntax and relative-link depth differences between formats. It also
rejects prompt-only settings and invocation-control flags in skills. Its
[fixture tests](build/tests/agent-customizations.tests.ps1) run as the first Definition-of-Done stage, including checks
against the real repository. Run them directly with `pwsh -NoProfile -File .\build\tests\agent-customizations.tests.ps1`.
After `./.template/Update-Project.ps1` brings in a new template version, run this check; for `audit-conventions`,
update both copies and run it. Skills may be selected automatically by the model; their descriptions and workflow
instructions guide their use. Manual-only selection is not a requirement of this design.

**Do not bulk-migrate these repository prompts.** Copilot's **Review Migrations** banner detects prompt files it
cannot read, but this repository already provides their skill equivalents. Use **Ignore** for the workspace
**Convert Prompt to Skills** group; **Show Ignored Migrations** restores it. This changes only the migration UI, not
which files the runtime can read. Generic conversion loses prompt-specific agent/model/tool selection and adds
`disable-model-invocation: true`, which is not part of our supported skill configuration. User-profile customizations
are separate: review any such entries individually rather than assuming the repository skills cover them. See
[ADR-0170](docs/adr/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md#dual-runtime-customization-contract).

## Build, test & contributing

Run [`./build/dod.ps1`](build/dod.ps1) — the single Definition of Done (tool-restore, CSharpier check, restore,
build + pack, test + coverage) that CI and developers both run. It fails fast on the first broken gate and prints
`Definition of Done: PASSED` as its final line on success — that line (not any generated file) is the pass signal.
For local development — including any protocol or validation step that runs the Definition of Done — use
`./build/dod.ps1 -Fast`: it parallelizes project builds and skips the non-correctness stages (coverage + packing) while
keeping compile warnings-as-errors and analyzers; test assemblies run in parallel in every mode. In the inner loop,
narrow the tests with `-Filter <expr>` (a `dotnet test --filter` expression, for example
`./build/dod.ps1 -Fast -Filter 'FullyQualifiedName~TokenEndpoint'`). `./build/dod.ps1 -Fast -Affected` is the **local
pre-push gate** (the push protocol's final check): it runs only the test projects the change reaches since its merge
base with `origin/main`, while still building the whole solution. The plain `./build/dod.ps1` is the full gate, which
runs in the merge queue, on `main` and on the release lines; a pull request into `main` runs it with `-Affected`
([ADR-0263](docs/adr/0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)). It is also
available locally for a thorough clean Release check. Package versions are centrally managed in
[`Directory.Packages.props`](Directory.Packages.props); versioning is stamped in lockstep by Nerdbank.GitVersioning
from the root [`version.json`](version.json). See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the full workflow and
[`GOVERNANCE.md`](GOVERNANCE.md) for ownership, versioning, and publishing. For a full conventions sweep, run the
[`/audit-conventions`](.github/prompts/audit-conventions.prompt.md) prompt.

This family is **greenfield and pre-release**: all work lands on the single `main` trunk through pull requests
([ADR-0125](docs/adr/0125-trunk-based-development-on-main.md),
[ADR-0261](docs/adr/0261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md)), breaking changes are
acceptable (prefer a clean cutover over compatibility shims, and expect `PublicAPI.Unshipped.txt` churn), and the design
follows an **Auth0-parity-plus** philosophy — familiar Auth0 shapes with more flexible, data-driven controls. See
[ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md). Because of this posture, **default to the
best long-term, cohesive design**: when a change forks between a minimal compatibility-preserving patch and the
comprehensive, correct model, choose the comprehensive model **without** weighing refactor difficulty or public-surface
breakage, and surface a fork for the user only when the options are genuinely equal in quality. Full statement: the
standing posture in [`BACKLOG.md`](BACKLOG.md).

## Copilot cloud agent sessions

A cloud-agent session stops at **59 minutes**, a hard limit that cannot be raised, and its runner is discarded with
everything on it. Only commits pushed from the repository clone survive — work on any other branch or `git worktree`
(for example under `/tmp`) is lost even when it is committed. So:

- **Size a session to fit.** Give one session one dependency chain of backlog items (one to three items), not a whole
  group. Run independent chains as separate sessions; each gets its own runner and its own 59 minutes.
- **Read the deadline from the runner, not the agent.** The limit is the job's `timeout-minutes: 59` in
  `copilot-setup-steps.yml`, so it counts from when the job starts and includes the setup steps; the agent can begin
  ten or more minutes in. `ps -o lstart= -p 1` prints when the runner booted, just before the job started, so it
  gives a safe estimate. Plan the session to end 59 minutes after that time, and start the final
  `./build/dod.ps1 -Fast -Affected` at least fifteen minutes before then.
- **Push each finished item at once**, from the clone, before starting the next. Do not leave finished work on a side
  branch or worktree to integrate later.
- **Re-author before merging.** The cloud agent authors its commits as `copilot-swe-agent[bot]`, so its pull request
  fails the required `Commit attribution` check until a person re-authors them
  (`git rebase origin/main --exec "git commit --amend --no-edit --reset-author"`) and force-pushes the branch
  (`git push --force-with-lease`). See
  [ADR-0128](docs/adr/0128-commits-are-attributed-to-people-not-agents.md) and
  [`.template/Test-CommitAttribution.ps1`](.template/Test-CommitAttribution.ps1).
- **Do not run parallel sub-agents that build.** The standard runner has four cores; concurrent .NET, pnpm, and Docker
  builds slow every agent down rather than finishing more items.
- **Budget the gates.** On the standard runner a cold `dotnet build` takes about two minutes and
  `./build/dod.ps1 -Fast -Affected` at most about five; iterate with targeted tests and run the pre-push gate once per
  item.
- **Validate the chart against a real API server.** `helm` and `kind` are on the runner, and a `kind` cluster starts in
  about a minute. Its API server checks the chart's manifests (`helm install --dry-run=server`), and Pod Security
  admission enforces a namespace labeled `restricted`. Pod-to-pod networking and cluster DNS do not work on the
  runner, so reach a pod with `kubectl port-forward` instead.

[`.github/workflows/copilot-setup-steps.yml`](.github/workflows/copilot-setup-steps.yml) installs the pinned .NET SDK,
Node, and pnpm and restores the NuGet and pnpm packages before a session starts. The platform runs it only from the
default branch, so until it is on `main` a session starts without pnpm or the frontend packages. Run
`npx -y pnpm@<version> install --frozen-lockfile` in `src/frontend`, using the version from `packageManager` in its
`package.json`; it takes seconds. Then run each tool through `npx -y pnpm@<version> exec`.
