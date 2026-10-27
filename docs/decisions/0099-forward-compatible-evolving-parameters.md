# 99. Forward-compatible evolving parameters: filter/criteria DTOs

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

Several abstractions have **growing sets of optional selection criteria**. `IGrantStore.GetPageAsync` and
`RevokeWhereAsync` are the clearest example: within one development cycle their filter grew from `(subjectId)` to
`(subjectId, clientId, sessionId)` to `(subjectId, clientId, sessionId, grantType)`. Each addition was a positional
parameter.

While the family is [pre-release](0025-pre-release-posture-and-auth0-parity-plus.md) adding a parameter is a free,
clean cutover. **After GA it is a breaking change**: a new positional (or even optional) parameter changes the method
signature, breaking source and binary compatibility for implementers and callers. For a published library whose store
interfaces consumers implement, that is a real cost.

## Decision

**Model an evolving set of optional selection criteria as a dedicated init-only DTO, not as positional parameters.**

- A criteria/filter DTO is a `readonly record struct` with **init-only, non-`required` properties** (e.g.
  `GrantFilter { SubjectId, ClientId, SessionId, GrantType }`). Adding a new criterion later is a _source-compatible_
  change: existing object initializers keep compiling, and the method signature is unchanged. Provide an `IsEmpty`-style
  guard where an empty filter would be dangerous (e.g. an unscoped bulk revoke).
- Construct via object initializer / `with`, **never** a positional primary constructor — adding a positional
  constructor parameter is itself breaking, which would defeat the purpose.

Apply this specifically to **optional, growing filter/criteria sets**. Do **not** dogmatically wrap everything:

- **Required identity keys stay positional** (e.g. the `clientId` a client-grant query is scoped to) — they are the
  operation's subject, not a growing filter.
- The **`(cursor, limit)` pagination pair is wrapped in a shared `PageRequest` DTO** (`NCode.Persistence.Stores`,
  beside `PagedResult<T>`): every store `GetPageAsync` takes `PageRequest { Cursor, Limit }`, so pagination can grow
  (e.g. a sort direction) without a breaking signature change — the same forward-compatible shape as the filter DTOs.
- **Request/command inputs already use init-only records** (`CreateSessionRequest`, `CreateSecurityTokenRequest`,
  mediator `*Command` records) — they are already forward-compatible and need no change.

## Consequences

- Store interfaces whose filters are expected to keep growing (grants today; more later) can gain criteria without a
  breaking signature change once GA lands.
- There is a single, memorable rule for contributors: **optional + growing → init-only DTO; required key → positional**
  (the formerly-positional `(cursor, limit)` pair is now the `PageRequest` DTO). This rule is also recorded in the
  production coding conventions.
- `IGrantStore` was refactored to `GetPageAsync(GrantFilter, PageRequest, ct)` and
  `RevokeWhereAsync(GrantFilter, revokedWhen, ct)`; the `PageRequest` wrapper was then swept across every store
  `GetPageAsync` (`IGrantStore`, `IClientStore`, `IClientGrantStore`, `IResourceServerStore`, `IServerStore`,
  `ITenantStore`, `ILocalAccountStore`, and the generic `BaseStore`). All callers and the management query surface were
  updated.

## References

- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the pre-release posture that makes _now_ the time to
  adopt the pattern before GA freezes the signatures.
- [`instructions/csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) —
  carries the convention for day-to-day work.
