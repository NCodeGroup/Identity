# 8. Conventions and lessons live in the repository, not in volatile memory

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode.Identity maintainers

## Context

Contributors — human and AI agents alike — accumulate durable knowledge while working: coding
conventions, the reasoning behind non-obvious choices, and recurring gotchas (analyzer quirks,
tooling edge cases). Where that knowledge is written down determines whether it survives.

An AI agent's private or session memory, a personal scratch file, or a chat transcript is
**volatile**: it is unversioned, unreviewable, invisible to teammates, not diffable alongside the
code it describes, and gone when the session ends or the memory is cleared. A rule that lives only
in someone's (or some agent's) head silently drifts from the code, and the same lesson gets
re-learned over and over.

## Decision

**All durable knowledge is captured in versioned repository files — never left only in an agent's or
an individual's volatile memory.** Each kind of knowledge has one home:

- A **convention** (a rule contributors follow) → the scoped instruction files under
  [`.github/instructions/`](../../.github/instructions/), which are the single source of truth and
  are auto-applied by path.
- The **reasoning** behind a non-obvious decision → an ADR in this directory.
- **Repo orientation / build / audit technique** → [`AGENTS.md`](../../AGENTS.md) and the
  [`/audit-conventions`](../../.github/prompts/audit-conventions.prompt.md) prompt.

When an agent or contributor learns something durable during a task, they **persist it to the
appropriate repository file as part of the same change** — they do not rely on private memory,
a wiki, or chat history as the system of record. Volatile memory may be used as a working scratchpad
within a task, but anything meant to outlive the task is promoted into the repo before the task is
considered done.

## Options considered

- **Agent / personal memory.** Rejected as the system of record: volatile, unversioned,
  unreviewable, unshared, and undiscoverable by the rest of the team or by CI.
- **An external wiki or doc site.** Rejected: it drifts from the code because it is not part of the
  same change, not diffed in review, and not enforced by the build.
- **Versioned repository files (chosen).** Reviewed in the same pull request as the code, versioned
  with it, discoverable via the instruction files, and — for the ✅ subset — enforced by the Definition
  of Done.

## Consequences

- Conventions and their reasoning are reviewable in pull requests, versioned with the code, and
  shared with everyone (and every future agent) automatically.
- The instruction files and ADRs stay the authoritative, single source of truth; there is no shadow
  rule-set in an agent's memory to fall out of sync.
- **"You chose wrong" smell:** a rule that is only enforced because an agent happens to remember it,
  or a lesson that exists solely in chat history, a personal note, or agent memory — promote it into
  the instruction files or an ADR, or it does not exist.

## References

- [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md) — points every session at
  the instruction files and ADRs as the sources of truth.
- The scoped instruction files under [`.github/instructions/`](../../.github/instructions/).
- [`AGENTS.md`](../../AGENTS.md) and [`CONTRIBUTING.md`](../../CONTRIBUTING.md).
