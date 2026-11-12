# 1170. Agent work is planned on Claude Opus and executed step by step on Claude Sonnet

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0003](project-template/0003-agent-work-is-planned-on-a-frontier-model-and-executed-on-a-cheaper-one.md)

## Context

[project-template ADR-0003](project-template/0003-agent-work-is-planned-on-a-frontier-model-and-executed-on-a-cheaper-one.md)
has a frontier model plan, re-plan and orchestrate, and a cheaper model execute one verified step at a time, sending any
step that needs a decision back to the frontier model. Its `PLAN.md` contract, its escalation, verification and review
rules, and its ownership of the workflow files by the template all hold here. This record fixes what the template
leaves to the project: the family's models, tools and runtime adapters.

The same split serves [`NCodeGroup/identity-platform`](https://github.com/NCodeGroup/identity-platform), whose
decisions are recorded in this repository.

Three forces shape the choices:

- Every contributor needs the whole loop, so every model is one a GitHub Copilot subscription offers, and no step
  depends on a second vendor account.
- The workflow runs in several tools: VS Code, Copilot CLI and Claude Code. Each loads customizations in its own
  format, and Claude Code ignores `applyTo`.
- Model names are literal strings. VS Code silently ignores an unknown model name and answers with the current model
  ([microsoft/vscode#319131](https://github.com/microsoft/vscode/issues/319131)), so a wrong name fails without a
  message.

## Decision

**Claude Opus 5.5 plans, re-plans, orchestrates and analyzes; Claude Sonnet 5.5 executes one verified step at a time
and reviews; a step that needs thinking goes back to Opus.** Every model is a GitHub Copilot built-in, so every
contributor has the whole loop:

| Role                                | Agents and prompts                                                                              | Model             |
| ----------------------------------- | ----------------------------------------------------------------------------------------------- | ----------------- |
| Plan, re-plan, orchestrate, analyze | Copilot Planner, Copilot Orchestrator, `/plan-*`, `/handoff-plan`, `/replan-step`, `/analyze-*` | Claude Opus 5.5   |
| Execute                             | Copilot Implementer, `/execute-step`, the `plan-executor` agent                                 | Claude Sonnet 5.5 |
| Review                              | Copilot Reviewer, `/review-plan-diff`, `/review-code`                                           | Claude Sonnet 5.5 |
| Execute a mechanical step           | The planner's **Execute next step (fast model)** handoff                                        | Claude Haiku 5.5  |

### Models and runtimes

- **The executor is a named model.** Claude Sonnet 5.5 is available in every session and runs every step on the same
  model. [`.vscode/settings.json`](../../.vscode/settings.json) routes the built-in Plan agent to Opus
  (`chat.planAgent.defaultModel`) and the built-in **Start Implementation** handoff to Sonnet
  (`github.copilot.chat.implementAgent.model`), so the built-in flow follows the same split.
- **The workflow runs in three tools.** In VS Code and in Copilot CLI, a planning session starts on Opus
  (`copilot --model claude-opus-5.5`). In Claude Code, each plan has its own git worktree under `.claude/worktrees/`,
  `CLAUDE.md` imports `AGENTS.md`, and `.claude/rules/` points Claude Code at the scoped instruction files by path,
  because it ignores `applyTo`.
- **A session that cannot delegate to the Sonnet executor selects Sonnet and runs `/execute-step` directly.** It
  returns to the planner for verification or re-planning. A session's model error is never a reason to hand-edit a
  generated agent.

### What the planner and the executor load

- **Repository rules reach every agent.** The planner and the executor both load
  [`repository.instructions.md`](../../.github/instructions/repository.instructions.md), [`AGENTS.md`](../../AGENTS.md)
  and the scoped instruction files, so a plan restates only what a step needs, and the Definition of Done stays the
  final gate.
- **The repository's instructions win.** `repository.instructions.md` wins over
  [`project-template.instructions.md`](../../.github/instructions/project-template.instructions.md), and the family's
  `csharp-production.instructions.md` and `csharp-testing.instructions.md` win over the template's generic C# files.
- **A workflow change is made in project-template** and pulled in with `/plan-template-update`. A repository-specific
  rule belongs in the instruction files, which every agent in the loop already loads.
- **`PLAN.md` never outlives its change.** [GitHub issues](https://github.com/NCodeGroup/identity/issues) track pending
  work and the ADRs carry the reasoning, so a plan's Decisions are promoted into ADRs, instruction files or issues
  before the plan's final review.

### Dual-runtime customization contract

**Local prompts and Copilot skills are both permanently supported.** The two files are runtime adapters of one
workflow, not competing definitions awaiting a migration. The adapter boundary is explicit:

- **Local** loads `.prompt.md` files and honors their prompt-specific `agent`, `model` and `tools` selections.
- **Copilot** sessions in VS Code and Copilot CLI load `SKILL.md`, not prompt files. A skill runs on the session's
  model, so Opus is selected for planning and orchestration and Sonnet for review. Execution uses the delegated
  executor, or the model selected before invoking `/execute-step` directly.
- **Prompt input placeholders** become the request supplied after the skill command, and relative Markdown links gain
  one directory level in skills. The descriptions and the ordered workflow bodies otherwise stay identical.
- **Skills stay available for automatic selection.** Their descriptions and workflow instructions guide appropriate
  invocation; they are not permission enforcement. Neither `disable-model-invocation` nor `user-invocable` is part of
  the contract.
- **Both copies appear in Local's slash menu,** which is an accepted consequence.

[`build/agent-customizations.ps1`](../../build/agent-customizations.ps1) checks every prompt and skill pair in both
directions: a missing counterpart, a mismatched name, description or body, malformed metadata, a prompt-only setting
in a skill, or an invocation-control flag fails with the offending path.
[`build/tests/agent-customizations.tests.ps1`](../../build/tests/agent-customizations.tests.ps1) runs as the first stage
of both Definition-of-Done modes.

Copilot's **Convert Prompt to Skills** migration group is ignored. The
[VS Code migration documentation](https://code.visualstudio.com/docs/agent-customization/migrate-customizations#convert-prompt-files-to-skills)
states that conversion drops prompt-specific agent, model and tool selection and adds `disable-model-invocation: true`,
a flag that has broken explicit skill invocation
([github/copilot-cli#4438](https://github.com/github/copilot-cli/issues/4438)). The migration notice identifies source
formats Copilot does not read; it is no proof that equivalent workflows are missing, because the skill adapters already
exist. User-profile migration entries are outside this contract and take individual review.

## Options considered

- **A local model, or a second vendor, as the default executor.** Cheaper per token for one person, but tied to one
  machine or one more account. Rejected as the repository default: Copilot is one billing and tooling surface that
  serves every contributor in VS Code and Copilot CLI; a contributor who also has Claude Code runs the same commands
  there.
- **Migrate everything to skills and remove the Local prompts.** Rejected: it discards Local's prompt-specific model,
  agent and tool selection without improving the Copilot workflows, whose skill adapters already exist.
- **Wait for manual-only skills before declaring dual support complete.** Rejected: automatic skill availability is an
  accepted behavior, not a missing capability that either runtime requires.
- **Opus plans, a named Sonnet executes, and the gate keeps the two adapters identical.** Chosen: every step runs on the
  same model in every session, and the customization gate keeps the prompt and skill copies from drifting.

## Consequences

- Model names are literal. When Copilot retires a model, the exact name from the `chat.planAgent.defaultModel` settings
  dropdown is set in project-template, and a response's hover text confirms the answering model. Copilot CLI uses model
  IDs such as `claude-opus-5.5`.
- Each workflow command appears twice in VS Code's slash menu, as a prompt file and as a skill. The customization gate
  prevents drift, so no contributor has to migrate away from either runtime.
- A skill runs on the session's model, so the person starting a session selects Opus before planning or orchestrating
  and Sonnet before reviewing.
- A step that needs a decision always returns to Opus, so the cheaper model never settles a design.

## References

- [`.github/agents/copilot-planner.agent.md`](../../.github/agents/copilot-planner.agent.md),
  [`copilot-implementer.agent.md`](../../.github/agents/copilot-implementer.agent.md),
  [`copilot-orchestrator.agent.md`](../../.github/agents/copilot-orchestrator.agent.md),
  [`copilot-reviewer.agent.md`](../../.github/agents/copilot-reviewer.agent.md),
  [`plan-executor.agent.md`](../../.github/agents/plan-executor.agent.md)
- [`AGENTS.md` — Plan-and-execute workflow](../../AGENTS.md#plan-and-execute-workflow)
- [project-template ADR-0001](project-template/0001-conventions-and-lessons-live-in-the-repository.md) — conventions and lessons live in the repository
- [project-template ADR-0018](project-template/0018-agent-attribution-is-avoided-not-enforced.md) — agent attribution is avoided, not enforced
- [ADR-1302](1302-the-main-branch-is-the-trunk-release-lines-service-shipped-versions-and-prereleases-use-trusted-publishing.md) — trunk-based development on `main` and its release lines
