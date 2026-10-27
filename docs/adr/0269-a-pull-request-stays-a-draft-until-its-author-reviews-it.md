# 269. A pull request stays a draft until its author reviews it

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** NCode Group

## Context

`main` accepts changes only through pull requests that pass the gate
([ADR-0261](0261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md),
[ADR-0263](0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)). The repository has one
maintainer, and GitHub does not let the author of a pull request approve it or request changes on it, so a
self-review can only be a comment. The push protocol also had the agent enable auto-merge as soon as it opened the
pull request.

That combination merged pull request #316 over an unresolved review comment. `main`'s ruleset requires every review
thread to be resolved (`required_review_thread_resolution`), but GitHub checks it when a pull request enters the merge
queue and not again before the queue merges: #316 entered the queue at 02:45:07, the comment arrived at 02:46:49, and
the queue merged it at 02:52:27 with the thread still open.

## Decision

The agent opens a pull request as a **draft** and never marks it ready or enables auto-merge. The author's self-review
is the gate:

1. **Request changes** is a comment left as an unresolved review thread. The thread rule keeps the pull request out of
   the merge queue.
2. **Approve** is resolving every thread, clicking **Ready for review**, and enabling auto-merge
   (`gh pr merge <number> --auto --squash`). Both are the author's actions.
3. A pull request that is already in the queue is removed from it before it is reviewed again or changed, because the
   thread rule is not rechecked in the queue.

The ruleset is unchanged: a draft cannot enter the merge queue, so the existing rules already enforce the sequence.

## Options considered

- **Open ready, enable auto-merge (the previous protocol).** Merges while the author is still reviewing. Rejected: it
  produced #316.
- **A workflow that submits an approving or requesting review when a label is applied.** Gives visible review states,
  but it is a ritual the author triggers on themselves and needs the Actions setting that lets workflows approve pull
  requests. Deferred: it can be added later without changing this decision.
- **A second account that reviews.** Gives the real buttons, but GitHub's terms allow one free personal account per
  person, with an exception for machine accounts that does not clearly cover a person reviewing by hand. Rejected.
- **Require one approving review in the ruleset.** The sole maintainer could not satisfy it. Rejected.

## Consequences

- An agent's pull request never enters the merge queue without a person's explicit action.
- Opening the pull request and merging it are two steps, so the author clicks **Ready for review** and enables
  auto-merge after reviewing.
- A review comment on a pull request that is already in the queue does not stop its merge until the pull request is
  removed from the queue.

## References

- [ADR-0261](0261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md) — pull requests are the only way into `main`.
- [ADR-0263](0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) — the merge queue runs the full gate.
- [ADR-0128](0128-commits-are-attributed-to-people-not-agents.md) — the author of a pull request is a person.
