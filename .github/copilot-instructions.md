# NCode.Identity — Copilot instructions

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
