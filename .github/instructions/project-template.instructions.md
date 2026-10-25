---
description: The plan-then-execute workflow and base conventions from the project template
applyTo: "**"
---

# Project Template Rules

These rules come from the project template. Template updates replace this file, so never edit it: to change a rule for this project, override it in `AGENTS.md` or in a project instruction file `AGENTS.md` lists. Where they differ, the project's instruction wins.

## Plan-then-execute

Work in this repository is planned by a frontier model and executed by cheaper ones, one verified step at a time.

- `PLAN.md` at the repository root is the handoff between them. The planning commands (`/plan-feature`, `/plan-bugfix`, …) write it in a fixed shape: Goal, Context for the executor, Decisions, Out of scope, Steps and Review checklist.
- In Claude Code, each plan lives in its own git worktree, so several sessions can work on different plans at once: the planning command creates `.claude/worktrees/<name>/` on the branch `worktree-<name>` and commits `PLAN.md` there, and the commands that execute, re-plan or review the plan work in that worktree. Its root is the plan's repository root. To continue the plan in another tool, open that folder.
- Each step is a `### [ ] N. Title` heading with Files, Change, Pattern and `- Done when:` lines. Markers: `[ ]` pending, `[x]` done, `[?]` escalated, `[!]` failed.
- A Done-when command is one PowerShell command, run from the repository root, that exits 0 only when its step is done. The build and test commands are in `AGENTS.md`.
- An executor changes only the files its step lists, runs the step's Done-when command, and marks the step `[x]` when it passes. When a step needs a decision the plan didn't make, it reverts its partial edits, marks the step `[?]`, adds an `- Escalation:` line and stops — it never improvises a design.
- A finished step is committed as `Plan step N: <title>`; a re-planned step as `Re-plan step N: <title>`.
- The workflow commands are in Claude Code (`.claude/commands/`), VS Code's Copilot Chat (`.github/prompts/` and `.github/agents/`) and Copilot CLI (`.github/skills/`), and `PLAN.md` is the only state they share: any of them can plan, execute, re-plan or review the same plan, whichever wrote the steps before.
- Never push, force-push or open a pull request unless asked. `git push` always asks for approval.
- Dependabot opens a pull request each week for newer versions of dependencies and CI actions; CI tests it, and a person reviews and merges it.

## Conventions

- UTF-8 without BOM and LF line endings; follow `.editorconfig`.
- Secrets never enter the repository or a prompt: `.env` files and `secrets/` are gitignored, and Claude Code is denied reading them.
- Commits and pull requests record the people who wrote them: never add a `Co-authored-by:` trailer naming an AI agent (Copilot, Claude or any other), a `Claude-Session:` trailer, or a "Generated with" line. Claude Code's `attribution` setting and VS Code's `git.addAICoAuthor` are off in this repository, and `.githooks/commit-msg` strips any that slip through. CI runs `.template/Test-CommitAttribution.ps1` on every pull request, merge queue entry and push to the default branch, and fails on a commit authored, committed or co-authored by an AI agent; to fix one, re-author it: `git rebase <base> --exec "git commit --amend --no-edit --reset-author"`.
- Pull requests: `gh pr create`, only when asked.

## Template Files and Project Files

- The project template owns these files, and template updates replace them, discarding any edit: this file, `.template/`, `.githooks/commit-msg`, `CLAUDE.md`, `.claude/agents/plan-executor.md`, the generated commands in `.claude/commands/`, the generated agents in `.github/agents/` and prompt files in `.github/prompts/`, the generated skills in `.github/skills/`, `.github/instructions/csharp.instructions.md`, `.claude/rules/csharp.md`, `build/ProjectTemplate.props`. `.copier-answers.yml` is Copier's. Never edit any of them. A command, prompt file, skill or agent the project adds to those folders under a name the template doesn't use is the project's, and updates leave it alone.
- Every other file belongs to the project, and can be edited. Git and editor settings, CI, Dependabot and Claude Code permissions, `global.json` and `Directory.Packages.props` also take later template changes, merged with the project's edits; the rest — `AGENTS.md`, `README.md`, `Directory.Build.props` and the project's own files — template updates never touch.
- To add or change an instruction, edit `AGENTS.md`, or add a file and list it there: `.github/instructions/<name>.instructions.md` for every agent, or `.claude/rules/<name>.md` for Claude Code only.
- To change a build setting, set it in `Directory.Build.props` after its import of `build/ProjectTemplate.props`.

## .NET

- `build/ProjectTemplate.props`, imported by `Directory.Build.props`, sets the shared build settings: latest C#, nullable reference types, implicit usings, and warnings as errors.
- The SDK version is pinned in `global.json`, and Dependabot proposes newer ones. Moving the target frameworks to a new .NET major version is a planned migration (`/plan-migration`).
- Every project belongs to the solution at the repository root, so `dotnet build` and `dotnet test` there cover all of them. Create it with `dotnet new sln` when adding the first project, and add each new project with `dotnet sln add <project folder>`.
- Package versions are in `Directory.Packages.props` (central package management); `<PackageReference>` items carry no `Version`. `dotnet add package` already writes the version there. `dotnet new` templates don't: they put `Version` on each `<PackageReference>`, which fails the build with NU1008 — move each version into a `<PackageVersion>` item in `Directory.Packages.props`.
- C# conventions: `.github/instructions/csharp.instructions.md`. Read it before editing `.cs` files.
