---
description: "NCode.Identity repository rules — design posture, orientation, commit attribution and the push protocol"
applyTo: "**"
---

# NCode.Identity — repository instructions

## Standing direction from the project owner

The project owner's words, which hold until the first stable release
([#535](https://github.com/NCodeGroup/identity/issues/535) retires them then):

> I want the best option long term that is cohesive and comprehensive, irrelevant of how difficult it would be to
> refactor now to accomplish that. This entire project is greenfield: it is in active development, nothing is shipped,
> breaking changes are allowed and must not be tracked (no temporal content).

This is a settled decision, not a question. Apply it without asking, and never re-argue it:

- **Choose the comprehensive design.** When the options are a minimal fix and the cohesive, comprehensive model, take
  the model. The size of the refactor, the number of files it touches, public API churn, renames and moved types are
  never reasons to settle for less. Decline a review finding only when it is wrong on the merits, never because the
  right fix is too much work. A partial fix is unfinished work, not a resting point.
- **Nothing is shipped, so there is nothing to keep compatible.** There are no released packages, deployed data,
  issued tokens or consumers to keep working. Never add or keep a compatibility shim, a reader for an older format, a
  transition path, "keeps working until it is rotated" behavior, or a rolling-deploy, rollback, upgrade or migration
  note for anything that has only existed in this repository.
- **Breaking changes are allowed and never tracked.** Every text describes the product as it is, never how it
  differs from an earlier state. That covers code, comments, docs, the changelog, decision records, test names,
  commit messages and pull requests. How:
  [The product is described as it is](#the-product-is-described-as-it-is--changes-are-not-tracked).
- **It binds everything an agent writes and everything it delegates.** It applies to the prompts and instructions an
  agent gives subagents, workflows and other sessions, so never instruct one to preserve compatibility or document an
  upgrade. A review bot's finding that asks for a breaking-change label, a compatibility note or a migration path is
  answered by citing this section. Where another instruction file, a template rule or a prompt says otherwise, this
  section wins.

## Design posture (greenfield, pre-release)

This family is **greenfield and pre-release** — nothing is shipped and there is **no backward-compatibility to
preserve** ([ADR-1025](../../docs/decisions/1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)). **Default to the best
long-term, cohesive design.** When a change forks between a minimal, compatibility-preserving patch and the
comprehensive, correct model, choose the comprehensive model **without** weighing refactor difficulty or public-surface
breakage — PublicAPI churn, setting renames/removals, signature changes, moved types, and clean cutovers are expected
and preferred over shims. Surface a fork for the user to decide **only** when the options are genuinely equal in quality
(a real value/security/semantics trade-off), not when one is merely more work.

### The product is described as it is — changes are not tracked

Breaking changes are allowed and **never tracked**: the family is a pre-release project, `pre-release: untracked` in
the [Landing](../../AGENTS.md#landing) section of `AGENTS.md`, as
[project-template ADR-0015](../../docs/decisions/project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)
decides.
Every text a change writes — a `CHANGELOG.md` entry, a document, a code comment, an XML doc, a decision record's text,
a test name, a commit message, a pull request's title and description — describes the product **as it is**: what a
package, type, setting, endpoint or behavior is and does, never how it differs from an earlier unreleased state.

- **No delta phrasing.** No `**Breaking:**` label; no "now", "new"/"newly" set against an earlier state, "no longer",
  "previously", "used to", "replaces"/"is replaced by", "instead of the old", "renamed", "moved", "migrated", "before
  this change" or "after this change"; no name of a removed or renamed member; no upgrade, migration, rollback,
  compatibility or "keeps working until" note. The product's own mechanisms keep their words: an EF Core migration, a
  key that rotation replaces and a new session are behavior, described as such.
- **Fixes are not tracked.** A fix to unreleased behavior records nothing about what was wrong; the behavior it
  establishes is described as a capability or a protection, and needs no text when the text already describes it.
- **The changelog is a catalog.** Until the first stable release, `## [Unreleased]` in
  [`CHANGELOG.md`](../../CHANGELOG.md) is a present-tense catalog of what the family does, under `### Added`
  (capabilities) and `### Security` (security posture and protections) only. A change edits it in place to match the
  code: it adds an entry, rewrites the entry whose capability it reshapes, and deletes the entry whose capability it
  removes, as the template's planning, review and landing commands do in a pre-release project.
- **No breaking marker.** Commit subjects and pull request titles never carry the conventional-commit `!` marker
  (`feat:`, never `feat!:`), and no commit message or pull request description carries a `BREAKING CHANGE:` footer.
- **Decision records are rewritten, not superseded.** A changed decision rewrites its record in place, and the
  record's only relation lines, `Refines`, `Supersedes` and `Conflicts with`, name project template records
  ([ADR-1503](../../docs/decisions/1503-a-decision-record-is-rewritten-when-its-decision-changes-and-relates-only-to-template-records.md));
  its Context states its forces in the present tense.

### Extensibility is the goal — nothing closed by default

The family exists to be extended easily and without restriction: a consumer or a sibling package adds a setting, a
setting scope, a security profile, a grant type, a client-authentication method, a claim, a store, or an endpoint by
**registering** it, never by editing the library. Every vocabulary is therefore **open by default** — string names plus
registered descriptors or strategies, validated as a whole when composed — and a closed construct (an `enum`, a
`switch` over a vocabulary, a hard-coded relationship between its members) must be justified as provably closed. Before
adding one, ask "could a package that is not this one ever need another member?"; if the answer is not a clear no, it
is open. The rule and its test: [`csharp-production.instructions.md`](csharp-production.instructions.md)
§2 and [ADR-1150](../../docs/decisions/1150-vocabularies-are-open-unless-provably-closed.md).

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

- Production code: [`instructions/csharp-production.instructions.md`](csharp-production.instructions.md), which refines
  the project template's [`csharp.instructions.md`](csharp.instructions.md)
- Test code (`*Tests` projects): [`instructions/csharp-testing.instructions.md`](csharp-testing.instructions.md), which
  refines the project template's [`csharp-tests.instructions.md`](csharp-tests.instructions.md)

The _reasoning_ behind a non-obvious decision lives in an ADR under [`docs/decisions/`](../../docs/decisions/).

Durable knowledge — conventions, reasoning, and recurring gotchas — is captured in these versioned
files, **never** left only in an agent's volatile memory or chat history: promote any lesson learned
during a task into the instruction files or an ADR as part of the same change. See
[project-template ADR-0001](../../docs/decisions/project-template/0001-conventions-and-lessons-live-in-the-repository.md).

Pending work — deferred decisions, feature gaps, follow-ups and review findings — is tracked as
[GitHub issues](https://github.com/NCodeGroup/identity/issues), the single source of truth for it
([ADR-1306](../../docs/decisions/1306-pending-work-is-tracked-as-github-issues-linked-by-address-in-files.md)). Record a pending item as an issue
labeled by its area (and a non-obvious decision in an ADR), not in chat history or volatile agent memory
([project-template ADR-0001](../../docs/decisions/project-template/0001-conventions-and-lessons-live-in-the-repository.md)). A pull request that completes an
issue closes it with `Closes #<number>`. Read open issues with `gh issue list`; reference one as
`[#123](https://github.com/NCodeGroup/identity/issues/123)` in a Markdown file, `issue #123` in a code comment, and
`#123` in an issue, a pull request or a commit message.

**Completion status is one fact, the same everywhere.** "Done" is binary and global: report an item as done — in a
message, a todo list, or by closing its issue — only when no work remains **and** nothing still frames it as pending
(an open issue, a todo list, a "remaining" / "still open" bullet in a doc, the `CHANGELOG.md`). Any remainder, even
"live verification remaining", makes it partial: say so, and edit its issue to describe only the remaining work.
Before claiming done, re-read the item's issue and reconcile every tracker first; the pull request that completes an
item closes its issue, so it is never both done and open.

## Shared machine resources

A development machine or runner has a few cores, which every agent and background job on it shares. A .NET build or
test run is CPU-bound, so two at once slow each other down. Integration tests that run beside another build time out
and fail with errors such as a client aborting its request after 10 s, which the code under test did not cause. So:

- **Run one build-heavy process at a time.** A build, a test run, `./build/dod.ps1` or a frontend gate waits for the
  one before it. A coordinator that delegates to several subagents lets only one of them build or test at a time.
  Reviewers that only read code run beside it, so instruct them to run no build, test or package-manager command.
- **Keep each build narrow.** Build with `-m:2`, run only the affected test projects (filtered where it helps), and
  leave the full Definition of Done to the push protocol's gate.
- **Leave no idle build servers.** Set `MSBUILDDISABLENODEREUSE=1` for agent builds, and run
  `dotnet build-server shutdown` when the work is done.
- **Re-run a test that fails under contention alone before you treat its failure as real.** A timeout in an
  integration test while another build ran is evidence of contention, not of a defect.
- **Never kill or find a process by a pattern that appears in your own command line.** `pkill -f '<pattern>'` or
  `pgrep -f '<pattern>'` inside a shell command matches that shell too. Bracket one character, as in
  `pkill -f '[M]SBuild.dll'`, or use the process ID.

## Commit attribution

**Never add agent attribution to a commit message** — a `Co-authored-by:` trailer naming an agent account (`Copilot`,
`copilot-swe-agent[bot]`, Claude's `noreply@anthropic.com`, or any other tooling identity), a `Claude-Session:` trailer,
or a "Generated with" line. A commit records the **people** who authored it; the repository host reads such a trailer as
authorship and lists the agent in the contributor graph, which can only be undone by rewriting published history.
Nothing checks for it or blocks a commit, push or pull request that has it, so keep it out at the source: do not
generate the lines. Claude Code's and VS Code's settings add none, and `/ship-plan` and `/run-issues` delete such
lines from the pull request title and description they write. See
[project-template ADR-0018](../../docs/decisions/project-template/0018-agent-attribution-is-avoided-not-enforced.md).

## Push protocol

When the user authorizes a push (says "push"), it pushes the current **topic branch** — never `main`, which accepts
changes only by pull request through the merge queue
([ADR-1261](../../docs/decisions/1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md),
[ADR-1263](../../docs/decisions/1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)) — and opens its
pull request against `main` as a **draft** if none exists (a push to an open pull request's branch updates it). For a
plan, `/ship-plan` does both, as the [Landing](../../AGENTS.md#landing) section of `AGENTS.md` says: its `draft: yes`
opens the pull request as a draft, and its `gate` runs gate 4 below before each push. Without a plan, push the branch
and run `gh pr create --draft`. The agent stops there: it never marks the pull request ready for review and never
enables auto-merge, because the user self-reviews the draft and that step is the approval
([ADR-1261](../../docs/decisions/1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md)). Run these gates
**before** pushing:

1. **Finish the plan.** If a `PLAN.md` exists, every step is `[x]`, its review (`/review-plan-diff`) has run with
   every queued fix step done, and its `Reviewed:` line matches the work. Gates 2 and 3 change files, so do them before
   that final review, or run the review again after them: `/ship-plan` lands only the reviewed tree. Keep `PLAN.md`:
   `/ship-plan` reads it and drops the empty `Plan: <title>` and `Re-plan step N` commits from a branch that was never
   pushed, and `/finish-plan` deletes it once the pull request merges. The `Plan step N` checkpoints reach the pull
   request's branch but never `main`: the merge queue squash-merges, taking the commit's subject from the pull request
   title, which `/ship-plan` takes from the plan's title, so a plan's title is a conventional commit subject
   ([ADR-1170](../../docs/decisions/1170-agent-work-is-planned-on-claude-opus-and-executed-step-by-step-on-claude-sonnet.md)).
2. **Persist lessons.** Promote every lesson learned in the session into durable repository content — a convention
   into the scoped instruction files, a non-obvious decision into an ADR under [`docs/decisions/`](../../docs/decisions/), a
   change to what the family does into the [`CHANGELOG.md`](../../CHANGELOG.md) catalog, and pending work into a
   GitHub issue (naming each issue the change completes as `Closes #<number>` in the pull request description).
3. **Drain volatile memory.** No durable knowledge may remain only in agent memory (`/memories/…`) or chat history:
   migrate anything durable into the repository and clear the memory so nothing survives solely as a volatile note.
4. **Pass the local pre-push gate.** Run `./build/dod.ps1 -Fast -Affected` — the local-development pre-push gate, which
   builds the whole solution and runs the test projects the change reaches since its merge base with `origin/main`
   (fetch `origin` first) — and confirm its final line is `Definition of Done: PASSED`. For a plan, `/ship-plan` runs it
   as the Landing section's `gate` and stops when it exits non-zero. CI owns the full `./build/dod.ps1` (all tests,
   coverage, packing, and NuGet advisories as errors): the merge queue runs it on the merged result before anything
   reaches `main`
   ([ADR-1263](../../docs/decisions/1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)), so it
   does **not** run before a local push. Neither gate builds the host image: when a change adds a file that a root
   build file imports, re-admit it in [`.dockerignore`](../../.dockerignore) too, or only the `Host image` and
   `Console image` checks on the pull request find it.
    - **The frontend has its own gate.** `./build/dod.ps1` never runs the frontend's checks. A change under
      `frontend` also runs CI's frontend job locally from `frontend`, with the pnpm version its `package.json`
      names: `pnpm install --frozen-lockfile`, then `pnpm run format:check`, `lint`, `typecheck`, `build` and
      `test:report`. `test:report` runs every package's tests with coverage, and a package under its coverage threshold
      fails the job.
    - **The end-to-end tier runs in CI.** `./build/dod.ps1` never runs the `*.EndToEnd.Tests` suites. The `End-to-end`
      check runs `./build/e2e.ps1`, which needs a running Docker engine. Run it locally to reproduce that check, as
      `./build/e2e.ps1 -Affected`.
    - **A merge conflict that only touches documentation skips the gate.** A push that only merges the base branch
      into a topic branch, and resolves conflicts only in documentation (Markdown files such as `CHANGELOG.md`,
      `docs/**` and the decision records), changes no code the gate checks. Resolve the conflicts, run the checks the
      resolved files have (`.template/Test-AdrIndex.ps1` for `docs/decisions/README.md`), and push without
      `./build/dod.ps1`. A merge that conflicts in any other file runs the gate.

The user then reviews the draft: a comment left as an unresolved thread requests changes, and resolving every thread,
clicking **Ready for review** and enabling auto-merge (`gh pr merge <number> --auto --squash`) approves it. Once
approved, the pull request merges through the merge queue, which runs the full gate on its merged result; to change a
pull request that is already in the queue, remove it from the queue first. An
item is done only when the merge queue merges the pull request and every GitHub Actions run for the resulting `main`
commit succeeds. Remediate any failure in the same change, and name every issue it completes with `Closes #<number>`
so the merge closes it. Once it merges, retire the plan's branch and worktree with `/finish-plan`
([`AGENTS.md`](../../AGENTS.md#plan-and-execute-workflow)).

This upholds [project-template ADR-0001](../../docs/decisions/project-template/0001-conventions-and-lessons-live-in-the-repository.md): conventions, reasoning,
and recurring gotchas live in the repository.
