# 1269. A pull request stays a draft until its author reviews it

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0006](project-template/0006-a-person-merges-every-pull-request.md); its merge method is [ADR-1304](1304-pull-requests-into-main-merge-by-squash-through-the-merge-queue.md)

## Context

[project-template ADR-0006](project-template/0006-a-person-merges-every-pull-request.md) has a person decide every
merge: an agent opens and updates pull requests and never merges one, marks it ready or turns on auto-merge. It leaves
opening every pull request as a draft to the project; this record takes that option.

`main` accepts changes only through pull requests that pass the gate
([ADR-1261](1261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md),
[ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)). The repository has one
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

## Consequences

- A review comment on a pull request that is already in the queue does not stop its merge until the pull request is
  removed from the queue.

## References

- [ADR-1261](1261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md) — pull requests are the only way into `main`.
- [ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) — the merge queue runs the full gate.
- [ADR-1128](1128-commits-are-attributed-to-people-not-agents.md) — the author of a pull request is a person.
