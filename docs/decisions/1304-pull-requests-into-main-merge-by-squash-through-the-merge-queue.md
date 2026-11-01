# 1304. Pull requests into main merge by squash through the merge queue

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Supersedes:** [project-template ADR-0013](project-template/0013-pull-requests-merge-with-a-merge-commit-or-by-rebasing.md)

## Context

[project-template ADR-0013](project-template/0013-pull-requests-merge-with-a-merge-commit-or-by-rebasing.md) makes a
merge commit or a rebase the way a pull request merges, and asks a project that squash-merges to supersede it in one
record. `main` accepts changes only through pull requests that pass the gate, its ruleset requires a linear history
([ADR-1261](1261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md)), and the merge queue runs the
full gate on the merged result
([ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)). A plan's title is a
conventional commit subject, and its `Plan step N` commits are checkpoints that each pass one step's check
([ADR-1170](1170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md)).

## Decision

A pull request into `main` merges by squash, through the merge queue. The queue tests the merged result and lands it as
one commit whose subject is the pull request's title and whose body is its description. No merge commit reaches
`main`.

- The required `Commit attribution` check is the guard
  ([ADR-1128](1128-commits-are-attributed-to-people-not-agents.md)): it reads the pull request's title and description
  that the squash commit takes, runs again when they are edited, and in the merge queue checks the commit that lands.
- A plan's `Plan step N` commits stay on the pull request's branch; `main` holds one commit per pull request.
- The merge queue's run on the merged result stands for a branch up to date with `main`.

## Options considered

- A merge commit or a rebase (project-template ADR-0013). Rejected: a merge commit breaks the linear history `main`
  requires, and a rebase lands each `Plan step N` checkpoint, which only its own step's check verified, as a commit of
  `main`.
- Squash through the merge queue (chosen): a linear `main` with one commit per pull request, each one the merged result
  the full gate passed, under a conventional commit subject.

## Consequences

- `git log` on `main` reads as one conventional commit per pull request; the step commits remain on the merged pull
  request.
- GitHub copies the co-author trailers of every branch commit and the pull request's description into the squash
  commit, so the `Commit attribution` check must stay required.
- [ADR-1261](1261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md),
  [ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) and
  [ADR-1269](1269-a-pull-request-stays-a-draft-until-its-author-reviews-it.md) refine project-template ADR-0005 and
  ADR-0006, which leave the merge method to this record.

## References

- [project-template ADR-0005](project-template/0005-the-default-branch-accepts-changes-only-through-pull-requests.md),
  [project-template ADR-0006](project-template/0006-a-person-merges-every-pull-request.md) and
  [project-template ADR-0013](project-template/0013-pull-requests-merge-with-a-merge-commit-or-by-rebasing.md).
- [`.github/workflows/commit-attribution.yml`](../../.github/workflows/commit-attribution.yml): the `Commit attribution`
  check.
- [NCodeGroup/project-template#200](https://github.com/NCodeGroup/project-template/issues/200).
