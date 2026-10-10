# NCode.Identity — Copilot instructions

## Design posture (greenfield, pre-release)

This family is **greenfield and pre-release** — nothing is shipped and there is **no backward-compatibility to
preserve** ([ADR-0025](../docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)). **Default to the best long-term,
cohesive design.** When a change forks between a minimal, compatibility-preserving patch and the comprehensive, correct
model, choose the comprehensive model **without** weighing refactor difficulty or public-surface breakage — PublicAPI
churn, setting renames/removals, signature changes, moved types, and clean cutovers are expected and preferred over
shims. Surface a fork for the user to decide **only** when the options are genuinely equal in quality (a real
value/security/semantics trade-off), not when one is merely more work. Full statement: the standing posture in
[`BACKLOG.md`](../BACKLOG.md).

## Orientation

Repo orientation and the build / contributing workflow are in [`AGENTS.md`](../AGENTS.md). All coding conventions are
the single source of truth in the scoped instruction files, auto-applied by path:

- Production code: [`instructions/csharp-production.instructions.md`](instructions/csharp-production.instructions.md)
- Test code (`*Tests` projects): [`instructions/csharp-testing.instructions.md`](instructions/csharp-testing.instructions.md)

The _reasoning_ behind a non-obvious decision lives in an ADR under [`docs/adr/`](../docs/adr/).

Durable knowledge — conventions, reasoning, and recurring gotchas — is captured in these versioned
files, **never** left only in an agent's volatile memory or chat history: promote any lesson learned
during a task into the instruction files or an ADR as part of the same change. See
[ADR-0008](../docs/adr/0008-conventions-and-lessons-live-in-the-repository.md).

Documented pending work (deferred decisions, feature gaps, follow-ups) is cataloged in
[`BACKLOG.md`](../BACKLOG.md) — the single source of truth for known pending work. Record pending items
there, not in chat history or volatile memory.

## Push protocol

When the user authorizes a push (says "push"), run these gates **before** pushing:

1. **Persist lessons.** Promote every lesson learned in the session into durable repository content — a convention
   into the scoped instruction files, a non-obvious decision into an ADR under [`docs/adr/`](../docs/adr/), a
   consumer-visible change into [`CHANGELOG.md`](../CHANGELOG.md), and pending work into [`BACKLOG.md`](../BACKLOG.md).
2. **Drain volatile memory.** No durable knowledge may remain only in agent memory (`/memories/…`) or chat history:
   migrate anything durable into the repository and clear the memory so nothing survives solely as a volatile note.
3. **Pass the CI-parity gate.** Run the full `./build/dod.ps1` — **not** `-Fast` (which is the local-development
   invocation) — and confirm its final line is `Definition of Done: PASSED`.

This upholds [ADR-0008](../docs/adr/0008-conventions-and-lessons-live-in-the-repository.md): conventions, reasoning, and
recurring gotchas live in the repository.
