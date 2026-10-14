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

### Security-practice default — decide, don't ask

Default **autonomously** to the modern, secure, recommended practice for OIDC / OAuth 2.x / identity platforms; do
**not** ask the user to adjudicate security design. The user has **no opinion** beyond "it must be modern, secure,
recommended practice" — so questions whose answer is settled by current best practice are noise, not a real fork.
Concretely, this means defaulting to the current authoritative guidance (OAuth 2.1 direction, the Security BCP
[RFC 9700], OpenID Connect Core/Discovery, FAPI where relevant): authorization-code **+ PKCE `S256`** for interactive
clients; **no** implicit, hybrid front-channel-token, or resource-owner-password flows; asymmetric token signing
(`PS256`/`ES256`, never `HS256` across trust boundaries); exact-match redirect URIs; sender-constraining and
short-lived tokens where applicable; least-privilege scopes; and secure-by-default headers/CORS. Only surface a
genuine value/semantics/security **trade-off** (two defensible modern options), never a question whose answer is just
"the recommended one".

**Treat existing repo code, comments, settings, and docs as point-in-time artifacts that may be wrong.** They are
evidence of intent, not a constraint: do not preserve an insecure or outdated shape merely because it is already
there. Correct it to the modern best-practice design (greenfield posture above), and update the surrounding
comments/docs/ADRs in the same change so the repository — not chat history — carries the correction.

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
[`BACKLOG.md`](../BACKLOG.md) — the single source of truth for known pending work, and the primary, default view of the
backlog (pending items only). Record pending items there, not in chat history or volatile memory. When an item is fully
done, **move** its text to the completed-items archive [`BACKLOG-COMPLETED.md`](../BACKLOG-COMPLETED.md) (don't just
delete it) so completed work stays discoverable.

## Push protocol

When the user authorizes a push (says "push"), run these gates **before** pushing:

1. **Persist lessons.** Promote every lesson learned in the session into durable repository content — a convention
   into the scoped instruction files, a non-obvious decision into an ADR under [`docs/adr/`](../docs/adr/), a
   consumer-visible change into [`CHANGELOG.md`](../CHANGELOG.md), and pending work into [`BACKLOG.md`](../BACKLOG.md)
   (moving any item completed this session into [`BACKLOG-COMPLETED.md`](../BACKLOG-COMPLETED.md)).
2. **Drain volatile memory.** No durable knowledge may remain only in agent memory (`/memories/…`) or chat history:
   migrate anything durable into the repository and clear the memory so nothing survives solely as a volatile note.
3. **Pass the local pre-push gate.** Run `./build/dod.ps1 -Fast` — the local-development pre-push gate — and confirm
   its final line is `Definition of Done: PASSED`. The full `./build/dod.ps1` (which adds coverage + packing and
   treats NuGet advisories as errors) is the CI-parity gate that runs in CI, **not** before a local push.

This upholds [ADR-0008](../docs/adr/0008-conventions-and-lessons-live-in-the-repository.md): conventions, reasoning, and
recurring gotchas live in the repository.
