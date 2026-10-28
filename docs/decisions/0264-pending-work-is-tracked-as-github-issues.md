# 264. Pending work is tracked as GitHub issues

- **Status:** Accepted
- **Date:** 2026-10-25
- **Deciders:** NCode.Identity maintainers

## Context

Pending work (deferred decisions, feature gaps, follow-ups and review findings) needs one home that every contributor
and agent reads. Two forces shape it:

- **Done must be one fact.** An item is either pending or complete, and every place that mentions it agrees. A home that
  links an item to the change that completes it makes that fact checkable.
- **The home must be filterable by area.** Contributors and agents look for the work in one area (conformance, the
  frontend, secrets, tooling) without reading everything else.

Conventions, reasoning and recurring gotchas stay in the repository
([ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md)); a tracker holds only work that is still to do.

## Decision

**GitHub issues in `NCodeGroup/Identity` are the single source of truth for pending work, one issue per item.**

1. **One issue per item, labeled by area.** The labels are `deferred`, `enhancement`, `conformance`, `frontend`,
   `secrets`, `tooling` and `review-finding`. A new area is a new label. `/ship-plan` labels each follow-up it files
   `follow-up` (the `follow-up-label` of `AGENTS.md`'s Landing section), which records that a plan found the item and
   left it; the item also takes its area label when it is triaged.
2. **An item is referenced by its issue number.** In Markdown it is
   `[#123](https://github.com/NCodeGroup/Identity/issues/123)`; in code comments and other files it is `issue #123`.
3. **The pull request that completes an item closes it** with `Closes #123` in its description. The merge closes the
   issue, so the item is done when the pull request merges.
4. **A partly done item stays open.** Its body is edited to describe only the remaining work.
5. **A non-obvious decision still gets an ADR**, which the issue links.

## Options considered

- **A versioned Markdown catalog with hand-allocated IDs.** Rejected: IDs collide across concurrent branches,
  completion means moving text between files, and nothing links an item to the pull request that closes it.
- **A project board alone.** Rejected: a board tracks issues, so it can be layered on top of them later.
- **GitHub issues (chosen).** Numbers are allocated by the host, so concurrent branches never collide. Closing is one
  state change that a merged pull request makes, and labels filter by area.

## Consequences

- Agents read pending work with `gh issue list` or the GitHub tools, and record new work with `gh issue create`.
- Issue numbers are shared with pull requests, so a number in a reference can name either; the link resolves to the
  right page.
- An item is done when the merged pull request closes its issue. An issue that is still open is pending, whatever a
  document says.
- A reference in a document or a code comment goes stale only if its issue is deleted, so issues are closed, never
  deleted.

## References

- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — durable knowledge lives in the repository.
- [ADR-0170](0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md) — the
  plan-and-execute workflow whose pull requests close issues.
- [`.github/instructions/repository.instructions.md`](../../.github/instructions/repository.instructions.md) — the
  agent rules that point at this tracker.
