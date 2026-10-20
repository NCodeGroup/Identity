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

### Extensibility is the goal — nothing closed by default

The family exists to be extended easily and without restriction: a consumer or a sibling package adds a setting, a
setting scope, a security profile, a grant type, a client-authentication method, a claim, a store, or an endpoint by
**registering** it, never by editing the library. Every vocabulary is therefore **open by default** — string names plus
registered descriptors or strategies, validated as a whole when composed — and a closed construct (an `enum`, a
`switch` over a vocabulary, a hard-coded relationship between its members) must be justified as provably closed. Before
adding one, ask "could a package that is not this one ever need another member?"; if the answer is not a clear no, it
is open. The rule and its test: [`csharp-production.instructions.md`](instructions/csharp-production.instructions.md)
§2 and [ADR-0150](../docs/adr/0150-vocabularies-are-open-unless-provably-closed.md).

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

**Pin what is current upstream, not what happens to be installed.** When choosing or pinning a version of a tool,
package, provider, or action, read the latest release from its upstream source (release feed or registry) at that
moment; a locally installed or remembered version says nothing about what is current.

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

**Completion status is one fact, the same everywhere.** "Done" is binary and global: report an item as done — in a
message, a todo list, or by archiving it — only when no work remains **and** nothing still frames it as pending
(`BACKLOG.md`, a todo list, a "remaining" / "still open" bullet in a doc, the `CHANGELOG.md`). Any remainder, even
"live verification remaining", makes it partial: say so, and rewrite its `BACKLOG.md` entry to describe only the
remaining work. Before claiming done, re-read the item's home and reconcile every tracker first; a completed item moves
to `BACKLOG-COMPLETED.md` in the same change, so it is never both done and listed.

## Commit attribution

**Never add a `Co-authored-by:` trailer naming an agent account** (`Copilot`, `copilot-swe-agent[bot]`, or any other
tooling identity) to a commit message. A commit records the **people** who authored it; the repository host reads that
trailer as authorship and lists the agent in the contributor graph, which can only be undone by rewriting published
history. The versioned [`.githooks/commit-msg`](../.githooks/commit-msg) hook strips such trailers as a safety net, but
it is enabled per clone and must not be relied on — do not generate the trailer in the first place. See
[ADR-0128](../docs/adr/0128-commits-are-attributed-to-people-not-agents.md).

## Push protocol

When the user authorizes a push (says "push"), run these gates **before** pushing:

1. **Retire the plan.** If a `PLAN.md` exists, every step is `[x]` and its review (`/review-plan-diff`) has run with
   every queued fix step done. Promote its Decisions in gate 2, delete `PLAN.md`, and squash its `Plan step N` /
   `Re-plan step N` checkpoint commits into the conventional commit(s) they belong to — neither the plan nor its
   checkpoints reach `origin`
   ([ADR-0170](../docs/adr/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md)).
2. **Persist lessons.** Promote every lesson learned in the session into durable repository content — a convention
   into the scoped instruction files, a non-obvious decision into an ADR under [`docs/adr/`](../docs/adr/), a
   consumer-visible change into [`CHANGELOG.md`](../CHANGELOG.md), and pending work into [`BACKLOG.md`](../BACKLOG.md)
   (moving any item completed this session into [`BACKLOG-COMPLETED.md`](../BACKLOG-COMPLETED.md)).
3. **Drain volatile memory.** No durable knowledge may remain only in agent memory (`/memories/…`) or chat history:
   migrate anything durable into the repository and clear the memory so nothing survives solely as a volatile note.
4. **Pass the local pre-push gate.** Run `./build/dod.ps1 -Fast` — the local-development pre-push gate — and confirm
   its final line is `Definition of Done: PASSED`. The full `./build/dod.ps1` (which adds coverage + packing and
   treats NuGet advisories as errors) is the CI-parity gate that runs in CI, **not** before a local push.

This upholds [ADR-0008](../docs/adr/0008-conventions-and-lessons-live-in-the-repository.md): conventions, reasoning, and
recurring gotchas live in the repository.
