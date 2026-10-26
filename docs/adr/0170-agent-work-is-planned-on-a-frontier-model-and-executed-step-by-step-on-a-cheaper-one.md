# 170. Agent work is planned on a frontier model and executed step by step on a cheaper one

- **Status:** Accepted (amended 2026-10-25)
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Amendment (2026-10-25): the project template, Claude Sonnet 5.5 and Claude Code

This repository adopted [project-template](https://github.com/polewskm/project-template) `v0.3.0` with its
`Adopt-Project.ps1` (backlog `X10`), so the workflow files are template-owned files of a Copier project rather than an
installer's output. Three parts of the decision change:

- **The files come from the template.** The agents, prompt files and skills under `.github/`, Claude Code's commands
  under `.claude/commands/`, `.claude/agents/plan-executor.md`, `CLAUDE.md`, `.githooks/commit-msg` and `.template/`
  are owned by project-template. [`.copier-answers.yml`](../../.copier-answers.yml) records the version, and
  `./.template/Update-Project.ps1` moves to a newer one. A workflow change is made and tagged in project-template,
  then pulled in; the installer command below no longer applies. The template's rules arrive in
  [`project-template.instructions.md`](../../.github/instructions/project-template.instructions.md), and this
  repository's instructions win where they differ.
- **The executor is Claude Sonnet 5.5,** not Auto: Copilot Implementer, `/execute-step`, the Copilot CLI
  `plan-executor` agent (`claude-sonnet-5.5`) and Claude Code's `plan-executor` subagent. Auto was not offered in
  every session — `plan-executor` failed to start with "Model 'auto' is not available" — and the model it picked
  varied from step to step. A named built-in model is available wherever Copilot is, and runs every step on the same
  model. `.vscode/settings.json` routes the built-in **Start Implementation** handoff to Sonnet as well.
- **Claude Code is supported** alongside VS Code and Copilot CLI. Copilot stays the surface every contributor has; a
  contributor with Claude Code runs the same commands there, and each Claude Code plan lives in its own git worktree
  under `.claude/worktrees/`. `CLAUDE.md` imports `AGENTS.md`, so the repository-wide rules moved from
  `.github/copilot-instructions.md`, which Claude Code does not read, to
  [`repository.instructions.md`](../../.github/instructions/repository.instructions.md), which `AGENTS.md` lists.

The commit-attribution check is now the template's `.template/Test-CommitAttribution.ps1`, and the template tests it
and the hook ([ADR-0128](0128-commits-are-attributed-to-people-not-agents.md)). identity-platform keeps the installer
until it adopts the template too (backlog `X11`).

The adoption kept the files the template merges with this repository's own (`.editorconfig`, `.gitattributes`,
`.gitignore`, `.vscode/settings.json`, `.claude/settings.json`, `Directory.Build.props` and `Directory.Packages.props`)
as the template writes them, so a later template update merges cleanly; only the duplicate
`ManagePackageVersionsCentrally` was removed, and the two `.props` files are formatted with CSharpier, which skips the
template-owned `build/ProjectTemplate.props`. `Directory.Build.props` imports that file first and keeps its own
settings after the import, so they win. The template's `nuget_package` answer is `false`, because
`Directory.Build.props` and `Directory.Build.targets` already own packaging. The `Commit attribution` check stays its
own workflow rather than the template's `ci.yml` job, because this repository keeps its own `ci.yml`. The template's
generic `csharp.instructions.md` applies too, and `csharp-production.instructions.md` and
`csharp-testing.instructions.md` win where they differ; `.claude/rules/` points Claude Code at them by path, because
it ignores `applyTo`.

## Context

An agent-driven change has two halves with opposite needs. **Planning** — reading the code paths, settling the design,
deciding exactly what each file must say — is a few turns that need deep reasoning. **Execution** — read, edit, build,
test, fix — is 20–60 tool turns per change, each resending the growing context, and is most of the token volume.
Running both halves on the strongest model spends its premium rate on typing; running both on a cheaper model spends
nothing on the design.

A cheaper model follows a precise instruction well and a vague one badly. When a step leaves a decision open, it
improvises — creates the function the plan assumed existed, reshapes an interface, edits a file the plan never named —
and still reports success. In this repository an improvised design choice is exactly what the scoped instruction files
and the ADRs exist to prevent.

The same split serves [`NCodeGroup/identity-platform`](https://github.com/NCodeGroup/identity-platform), whose
decisions are recorded here.

## Decision

**A frontier model plans, re-plans, and orchestrates; a lower-cost model executes one verified step at a time; any
step that needs thinking goes back to the frontier model.** Every model is a GitHub Copilot built-in, so every
contributor has the whole loop in VS Code and in Copilot CLI:

| Role                                | Agents and prompts                                                                              | Model             |
| ----------------------------------- | ----------------------------------------------------------------------------------------------- | ----------------- |
| Plan, re-plan, orchestrate, analyze | Copilot Planner, Copilot Orchestrator, `/plan-*`, `/handoff-plan`, `/replan-step`, `/analyze-*` | Claude Opus 5.5   |
| Execute                             | Copilot Implementer, `/execute-step`, the Copilot CLI `plan-executor` agent                     | Auto              |
| Execute a mechanical step           | The planner's **Execute next step (fast model)** handoff                                        | Claude Haiku 5.5  |
| Review                              | Copilot Reviewer, `/review-plan-diff`, `/review-code`                                           | Claude Sonnet 5.5 |

The rules:

- **`PLAN.md` is the contract.** At the repository root: Goal, Context for the executor, Decisions, Out of scope,
  numbered `### [ ] N.` steps that name exact files and symbols, each ending in a `- Done when:` PowerShell command
  that exits 0 only when that step is done, and a Review checklist. The executor sees only `PLAN.md` and the files it
  opens, so every design decision is made in the plan.
- **The executor never redesigns.** When finishing a step would need a design choice, a file or interface the step does
  not list, or the plan contradicts the code, the executor reverts its partial edits, marks the step `[?]` with an
  `- Escalation:` line, and stops.
- **Re-planning runs on Opus.** The planner — or the orchestrator, which is itself the planner — diagnoses the step,
  rewrites or splits it, records any redesign under Decisions, resets it to `[ ]`, and hands it back. After two
  re-plans of the same step the loop stops and asks the person driving it.
- **The coordinator verifies.** The orchestrator runs each step's Done-when command itself before marking it `[x]` and
  committing a `Plan step N: <title>` checkpoint; an executor's report of success is not the check.
- **Review is part of the loop.** The reviewer compares the diff with the plan, rates every deviation from a step's
  text at least should-fix — a deviation means the executor improvised instead of escalating — and appends fixes as new
  steps for the executor.
- **`PLAN.md` and its checkpoints are transient.** [`BACKLOG.md`](../../BACKLOG.md) is the single tracker of pending
  work and the ADRs carry the reasoning, so a plan never outlives its change. `PLAN.md` is a gitignored working file
  that no commit contains, and each step's checkpoint stages only the files the step lists. Its Decisions are promoted
  into ADRs, instruction files, or the backlog, `PLAN.md` is deleted, and the `Plan` / `Plan step` / `Re-plan step`
  checkpoints are squashed into conventional commits before the change leaves the machine — before its topic branch is
  pushed here ([ADR-0261](0261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md)), and before a
  pull request merges in `identity-platform`.
- **Repository rules reach every agent.** The planner and the executor both load
  [`repository.instructions.md`](../../.github/instructions/repository.instructions.md), [`AGENTS.md`](../../AGENTS.md),
  and the scoped instruction files, so a plan restates only what a step needs, and the Definition of Done stays the
  final gate.

The agents ([`.github/agents/`](../../.github/agents/)), prompt files ([`.github/prompts/`](../../.github/prompts/)),
and Copilot CLI skills ([`.github/skills/`](../../.github/skills/)) are generated from the maintainer's plan-and-execute
template set by its installer:

```pwsh
Install-HybridWorkflow.ps1 -Repo . -Variant Copilot -Target VSCode, CopilotCli -CliExecutorModel auto
```

A change to the workflow is made in the templates and re-installed, never hand-edited into a generated copy; a
repository-specific rule belongs in the instruction files, which every agent in the loop already loads. The
`audit-conventions` prompt and skill are hand-written and outside the generated set.
[`.vscode/settings.json`](../../.vscode/settings.json) routes VS Code's built-in Plan agent to Opus and its **Start
Implementation** handoff to Auto, so the built-in flow follows the same split.

### Dual-runtime customization contract

**Retain both Local prompts and Copilot skills as the permanent supported design**, closing backlog item `X1`.
The two files are runtime adapters for one workflow, not competing definitions awaiting a migration. The 16 generated
workflow pairs come from the installer templates; the hand-written `audit-conventions` pair is maintained together.
The adapter boundary is explicit:

- **Local** loads `.prompt.md` files and honors their prompt-specific `agent`, `model`, and `tools` selections.
- **Copilot** sessions in VS Code and Copilot CLI load `SKILL.md`, not prompt files. Skills run on the current session
  model, so select Opus for planning/orchestration and Sonnet for review. Execution uses the delegated executor, or
  the model selected before invoking `/execute-step` directly. Model availability belongs to the session: if it
  cannot delegate to the configured Auto executor, the supported fallback is to select an available lower-cost model
  and invoke `/execute-step` directly, then return to the planner for verification or re-planning. A session-specific
  model error is not a reason to rewrite generated agents or migrate prompt files.
- Prompt input placeholders become the request supplied after the skill command. Relative Markdown links gain one
  directory level in skills. The descriptions and ordered workflow bodies otherwise stay identical.
- Skills remain available for automatic selection. Their descriptions and workflow instructions guide appropriate
  invocation; they are not permission enforcement. Neither `disable-model-invocation` nor `user-invocable` is part
  of this contract. Duplicate prompt/skill commands in Local's slash menu are an accepted consequence.

[`build/agent-customizations.ps1`](../../build/agent-customizations.ps1) checks every project prompt and skill in both
directions: a missing counterpart, mismatched name/description/body, malformed scalar metadata, a prompt-only skill
setting, or an invocation-control flag fails with the offending path. Comparison preserves line order and code;
only argument substitution, relative inline Markdown links outside code, line endings, and outer whitespace are
adapted. The validator intentionally accepts the scalar frontmatter format used by these files, not arbitrary YAML.
[`build/tests/agent-customizations.tests.ps1`](../../build/tests/agent-customizations.tests.ps1) tests the actual pairs
and isolated success/failure fixtures; it runs as the first stage of both Definition-of-Done modes. Template
reinstallation must satisfy the same gate, and the hand-written pair cannot silently drift.

Copilot's migration notice identifies source formats it does not read, not proof that equivalent workflows are
missing. Ignore the workspace prompt-conversion group because its skill adapters already exist; do not run generic
conversion over them. The
[VS Code migration documentation](https://code.visualstudio.com/docs/agent-customization/migrate-customizations#convert-prompt-files-to-skills)
states that conversion drops prompt-specific agent/model/tool selection and adds `disable-model-invocation: true`.
That flag has also made explicit skill invocation fail
([github/copilot-cli#4438](https://github.com/github/copilot-cli/issues/4438), still open when checked on 2026-10-20).
The bug explains the configuration choice, but its resolution is **not** a completion dependency or a scheduled
follow-up: automatic skill availability is an intentional supported behavior. Any future change is a new design
decision, not unfinished `X1` work. User-profile migration entries are outside this repository contract and require
individual review.

## Options considered

- **Opus for everything.** The best turn quality, but execution — most of the tokens — runs at the premium rate.
  Rejected: the volume is in the typing, not the thinking.
- **A cheaper model for everything.** The lowest cost, but the design is improvised turn by turn, and the conventions
  and ADRs are applied only as well as that model infers them. Rejected: the design is where quality is decided.
- **VS Code's built-in Plan agent and Start Implementation alone.** No files to maintain, but the implementer carries the
  whole planning conversation, runs the plan in one pass with no per-step check, and has no path back to the planner.
  Kept as a convenience through the settings, not as the main flow.
- **A local model as executor, or Claude Code with its own subscription.** Cheaper per token for one person, but tied to
  one machine or a second vendor account. Rejected as the repository default: one billing and tooling surface —
  Copilot — serves every contributor in both VS Code and Copilot CLI. The template set still installs those variants
  per user.
- **Migrate everything to skills and remove Local prompts.** Rejected: it discards Local's prompt-specific model,
  agent, and tool selection without improving the Copilot workflows, whose skill adapters already exist.
- **Wait for manual-only skills before declaring dual support complete.** Rejected: automatic skill availability is
  an accepted behavior, not a missing capability required by either supported runtime.

## Consequences

- Most tokens run at the executor's rate; Opus is spent on the plan, a short check per step, and re-plans.
- Plan precision is the lever. Every escalation is a round trip and a signal that a step was underspecified; the fix to
  one step usually sharpens the later ones.
- An improvised step that passes its own check is caught only by review, so a change is not done until the reviewer has
  run and its fix steps are `[x]`.
- Model names are literal. VS Code silently ignores an unknown model name and answers with the current model
  ([microsoft/vscode#319131](https://github.com/microsoft/vscode/issues/319131)), so when Copilot renames or retires a
  model, re-run the installer with the exact names from the `chat.planAgent.defaultModel` settings dropdown and confirm
  the answering model by hovering a response. Copilot CLI uses model IDs (`claude-opus-5.5`, `auto`).
- Copilot CLI skills run on the session's model, so a planning session starts on Opus
  (`copilot --model claude-opus-5.5`); `/orchestrate-plan` then delegates execution to `plan-executor` on Auto.
- Each workflow command appears twice in VS Code's slash menu — a prompt file for VS Code and a skill for Copilot CLI —
  the same pairing `audit-conventions` uses. This is intentional; the customization gate prevents drift, rather than
  requiring users to migrate away from either runtime.

## References

- [`.github/agents/copilot-planner.agent.md`](../../.github/agents/copilot-planner.agent.md),
  [`copilot-implementer.agent.md`](../../.github/agents/copilot-implementer.agent.md),
  [`copilot-orchestrator.agent.md`](../../.github/agents/copilot-orchestrator.agent.md),
  [`copilot-reviewer.agent.md`](../../.github/agents/copilot-reviewer.agent.md),
  [`plan-executor.agent.md`](../../.github/agents/plan-executor.agent.md)
- [`AGENTS.md` — Plan-and-execute workflow](../../AGENTS.md#plan-and-execute-workflow)
- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — conventions and lessons live in the repository
- [ADR-0125](0125-trunk-based-development-on-main.md) — trunk-based development on `main`
- [ADR-0128](0128-commits-are-attributed-to-people-not-agents.md) — commits are attributed to people, not agents
