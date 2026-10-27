---
description: "NCode.Identity repository rules — design posture, orientation, commit attribution and the push protocol"
applyTo: "**"
---

# NCode.Identity — repository instructions

## Design posture (greenfield, pre-release)

This family is **greenfield and pre-release** — nothing is shipped and there is **no backward-compatibility to
preserve** ([ADR-0025](../../docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)). **Default to the best
long-term, cohesive design.** When a change forks between a minimal, compatibility-preserving patch and the
comprehensive, correct model, choose the comprehensive model **without** weighing refactor difficulty or public-surface
breakage — PublicAPI churn, setting renames/removals, signature changes, moved types, and clean cutovers are expected
and preferred over shims. Surface a fork for the user to decide **only** when the options are genuinely equal in quality
(a real value/security/semantics trade-off), not when one is merely more work.

### Extensibility is the goal — nothing closed by default

The family exists to be extended easily and without restriction: a consumer or a sibling package adds a setting, a
setting scope, a security profile, a grant type, a client-authentication method, a claim, a store, or an endpoint by
**registering** it, never by editing the library. Every vocabulary is therefore **open by default** — string names plus
registered descriptors or strategies, validated as a whole when composed — and a closed construct (an `enum`, a
`switch` over a vocabulary, a hard-coded relationship between its members) must be justified as provably closed. Before
adding one, ask "could a package that is not this one ever need another member?"; if the answer is not a clear no, it
is open. The rule and its test: [`csharp-production.instructions.md`](csharp-production.instructions.md)
§2 and [ADR-0150](../../docs/decisions/0150-vocabularies-are-open-unless-provably-closed.md).

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

Repo orientation and the build / contributing workflow are in [`AGENTS.md`](../../AGENTS.md). All coding conventions are
the single source of truth in the scoped instruction files, auto-applied by path:

- Production code: [`instructions/csharp-production.instructions.md`](csharp-production.instructions.md)
- Test code (`*Tests` projects): [`instructions/csharp-testing.instructions.md`](csharp-testing.instructions.md)

The _reasoning_ behind a non-obvious decision lives in an ADR under [`docs/decisions/`](../../docs/decisions/).

Durable knowledge — conventions, reasoning, and recurring gotchas — is captured in these versioned
files, **never** left only in an agent's volatile memory or chat history: promote any lesson learned
during a task into the instruction files or an ADR as part of the same change. See
[ADR-0008](../../docs/decisions/0008-conventions-and-lessons-live-in-the-repository.md).

Pending work — deferred decisions, feature gaps, follow-ups and review findings — is tracked as
[GitHub issues](https://github.com/NCodeGroup/Identity/issues), the single source of truth for it
([ADR-0264](../../docs/decisions/0264-pending-work-is-tracked-as-github-issues.md)). Record a pending item as an issue
labeled by its area (and a non-obvious decision in an ADR), not in chat history or volatile agent memory
([ADR-0008](../../docs/decisions/0008-conventions-and-lessons-live-in-the-repository.md)). A pull request that completes an
issue closes it with `Closes #<number>`. Read open issues with `gh issue list`; reference one as
`[#123](https://github.com/NCodeGroup/Identity/issues/123)` in Markdown and as `issue #123` elsewhere.

**Completion status is one fact, the same everywhere.** "Done" is binary and global: report an item as done — in a
message, a todo list, or by closing its issue — only when no work remains **and** nothing still frames it as pending
(an open issue, a todo list, a "remaining" / "still open" bullet in a doc, the `CHANGELOG.md`). Any remainder, even
"live verification remaining", makes it partial: say so, and edit its issue to describe only the remaining work.
Before claiming done, re-read the item's issue and reconcile every tracker first; the pull request that completes an
item closes its issue, so it is never both done and open.

## Commit attribution

**Never add agent attribution to a commit message** — a `Co-authored-by:` trailer naming an agent account (`Copilot`,
`copilot-swe-agent[bot]`, Claude's `noreply@anthropic.com`, or any other tooling identity), a `Claude-Session:` trailer,
or a "Generated with" line. A commit records the **people** who authored it; the repository host reads such a trailer as
authorship and lists the agent in the contributor graph, which can only be undone by rewriting published history. The
versioned [`.githooks/commit-msg`](../../.githooks/commit-msg) hook strips these lines as a safety net, but it is
enabled per clone and must not be relied on — do not generate them in the first place. The required `Commit attribution`
check ([`.template/Test-CommitAttribution.ps1`](../../.template/Test-CommitAttribution.ps1), from the project template)
rejects agent-authored commits and agent attribution on pull requests, in the merge queue and on pushes, because a
squash merge credits every commit author. See
[ADR-0128](../../docs/decisions/0128-commits-are-attributed-to-people-not-agents.md).

## Push protocol

When the user authorizes a push (says "push"), it pushes the current **topic branch** — never `main`, which accepts
changes only by pull request through the merge queue
([ADR-0261](../../docs/decisions/0261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md),
[ADR-0263](../../docs/decisions/0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)) — and opens its
pull request against `main` as a **draft** if none exists (a push to an open pull request's branch updates it). For a
plan, `/ship-plan draft` does both, as the [Landing](../../AGENTS.md#landing) section of `AGENTS.md` says; keep
`draft`, because without it `/ship-plan` opens a pull request ready for review. Without a plan, push the branch and
run `gh pr create --draft`. The agent stops there: it never marks the pull request ready for review and never enables
auto-merge, because the user self-reviews the draft and that step is the approval
([ADR-0269](../../docs/decisions/0269-a-pull-request-stays-a-draft-until-its-author-reviews-it.md)). Run these gates
**before** pushing:

1. **Finish the plan.** If a `PLAN.md` exists, every step is `[x]`, its review (`/review-plan-diff`) has run with
   every queued fix step done, and its `Reviewed:` line matches the work. Gates 2 and 3 change files, so do them before
   that final review, or run the review again after them: `/ship-plan` lands only the reviewed tree. Keep `PLAN.md`:
   `/ship-plan` reads it and drops the empty `Plan: <title>` and `Re-plan step N` commits from a branch that was never
   pushed, and `/finish-plan` deletes it once the pull request merges. The `Plan step N` checkpoints reach the pull
   request's branch but never `main`: the merge queue squash-merges, taking the commit's subject from the pull request
   title, which `/ship-plan` takes from the plan's title, so a plan's title is a conventional commit subject
   ([ADR-0170](../../docs/decisions/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md)).
2. **Persist lessons.** Promote every lesson learned in the session into durable repository content — a convention
   into the scoped instruction files, a non-obvious decision into an ADR under [`docs/decisions/`](../../docs/decisions/), a
   consumer-visible change into [`CHANGELOG.md`](../../CHANGELOG.md), and pending work into a
   GitHub issue (naming each issue the change completes as `Closes #<number>` in the pull request description).
3. **Drain volatile memory.** No durable knowledge may remain only in agent memory (`/memories/…`) or chat history:
   migrate anything durable into the repository and clear the memory so nothing survives solely as a volatile note.
4. **Pass the local pre-push gate.** Run `./build/dod.ps1 -Fast -Affected` — the local-development pre-push gate, which
   builds the whole solution and runs the test projects the change reaches since its merge base with `origin/main`
   (fetch `origin` first) — and confirm its final line is `Definition of Done: PASSED`. CI owns the full
   `./build/dod.ps1` (all tests, coverage, packing, and NuGet advisories as errors): the merge queue runs it on the
   merged result before anything reaches `main`
   ([ADR-0263](../../docs/decisions/0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)), so it
   does **not** run before a local push. Neither gate builds the host image: when a change adds a file that a root
   build file imports, re-admit it in [`.dockerignore`](../../.dockerignore) too, or only the `Host image` check on the
   pull request finds it.

The user then reviews the draft: a comment left as an unresolved thread requests changes, and resolving every thread,
clicking **Ready for review** and enabling auto-merge (`gh pr merge <number> --auto --squash`) approves it. Once
approved, the pull request merges through the merge queue, which runs the full gate on its merged result; to change a
pull request that is already in the queue, remove it from the queue first. An
item is done only when the merge queue merges the pull request and every GitHub Actions run for the resulting `main`
commit succeeds. Remediate any failure in the same change, and name every issue it completes with `Closes #<number>`
so the merge closes it. Once it merges, retire the plan's branch and worktree with `/finish-plan`
([`AGENTS.md`](../../AGENTS.md#plan-and-execute-workflow)).

This upholds [ADR-0008](../../docs/decisions/0008-conventions-and-lessons-live-in-the-repository.md): conventions, reasoning,
and recurring gotchas live in the repository.
