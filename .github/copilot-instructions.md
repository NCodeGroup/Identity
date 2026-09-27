# NCode.Identity — Copilot instructions

Repo orientation and the build / contributing workflow are in [`AGENTS.md`](../AGENTS.md). All coding conventions are
the single source of truth in the scoped instruction files, auto-applied by path:

- Production code: [`instructions/csharp-production.instructions.md`](instructions/csharp-production.instructions.md)
- Test code (`*Tests` projects): [`instructions/csharp-testing.instructions.md`](instructions/csharp-testing.instructions.md)

The _reasoning_ behind a non-obvious decision lives in an ADR under [`docs/adr/`](../docs/adr/).
