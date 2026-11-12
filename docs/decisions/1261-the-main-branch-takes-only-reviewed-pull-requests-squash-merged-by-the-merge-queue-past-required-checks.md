# 1261. The main branch takes only reviewed pull requests, squash-merged by the merge queue past required checks

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0005](project-template/0005-the-default-branch-accepts-changes-only-through-pull-requests.md), [project-template ADR-0006](project-template/0006-a-person-merges-every-pull-request.md)
- **Supersedes:** [project-template ADR-0013](project-template/0013-pull-requests-merge-with-a-merge-commit-or-by-rebasing.md)

## Context

A direct push is the one path by which an unchecked commit would reach the trunk. The family therefore protects `main`
so that every change reaches it through a pull request that has passed the gate, including a cloud agent's `copilot/*`
branch and a Dependabot pull request.

[project-template ADR-0013](project-template/0013-pull-requests-merge-with-a-merge-commit-or-by-rebasing.md) makes a
merge commit or a rebase the way a pull request merges, and asks a project that squash-merges to supersede it in one
record. A plan's title is a conventional commit subject, and its `Plan step N` commits are checkpoints that each pass
one step's check ([ADR-1170](1170-agent-work-is-planned-on-claude-opus-and-executed-step-by-step-on-claude-sonnet.md)).
The merge queue runs the full gate on the merged result
([ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)).

## Decision

`main` accepts changes only through pull requests. A pull request merges by squash through the merge queue, and an
agent opens it as a draft that its author reviews before it can merge.

1. **The ruleset.** The repository ruleset `main` targets the default branch, is active and has no bypass actors. Its
   rules are `deletion`, `non_fast_forward`, `required_linear_history`, `pull_request` (0 required approvals, stale
   reviews dismissed on push, every review thread resolved), `required_status_checks` without the strict up-to-date
   policy, `merge_queue` and `code_scanning` (CodeQL).
2. **The merge queue.** It squash-merges, groups only all-green entries (`ALLGREEN`), builds and merges at most 5
   entries at a time and times out a check after 60 minutes. Its run on the merged result stands for a branch that is
   up to date with `main`.
3. **The required checks** are the jobs that build, test, lint and package the family: `Definition of Done`,
   `Lint, typecheck, test, build`, `Lint and render the host chart`, `Build host image`, `Build console image` and
   `End-to-end`. `GOVERNANCE.md` lists them. Codecov's statuses are not required, because a third party's outage would
   block every merge, and `copilot-setup-steps` is not required, because it reports only when its own workflow file
   changes.
4. **A required check reports on every pull request.** A path-filtered workflow would leave its check pending, so
   Frontend CI decides inside its `Detect frontend changes` job whether `Lint, typecheck, test, build` runs, and a
   skipped job reports success. A gating workflow added later joins the ruleset and reports on every pull request and
   on `merge_group`.
5. **Repository settings match the ruleset.** Merge commits are disabled; a squash commit takes the pull request's
   title (`PR_TITLE`) and description (`PR_BODY`); head branches are deleted on merge; auto-merge and "update branch"
   are enabled.
6. **The squash commit.** A pull request lands as one commit whose subject is the pull request title (a plan's
   conventional-commit title) and whose body is its description. A plan's `Plan step N` checkpoints stay on the pull
   request's branch, and `git log` on `main` reads one conventional commit per pull request. GitHub copies the
   co-author trailers of every branch commit into the squash commit, so a trailer a tool adds reaches `main`; nothing
   checks for it ([project-template ADR-0018](project-template/0018-agent-attribution-is-avoided-not-enforced.md)).
7. **Drafts and approval.** An agent opens every pull request as a draft and never marks it ready or enables
   auto-merge. A draft cannot enter the merge queue, so the ruleset enforces the sequence. The author requests changes
   by leaving an unresolved review thread, and approves by resolving every thread, clicking **Ready for review** and
   enabling auto-merge (`gh pr merge <number> --auto --squash`).
8. **Threads are checked on entry only.** GitHub checks thread resolution when a pull request enters the merge queue
   and not again before the queue merges it, so a pull request already in the queue is removed from it before it is
   reviewed again or changed.
9. **An approval requirement of one** is added when a second maintainer exists.

## Options considered

- **An unprotected trunk.** Rejected: a direct push skips the gate that CI already runs.
- **Classic branch protection.** Rejected: a ruleset is GitHub's current, visible and composable mechanism.
- **Merge commits.** Rejected: they break the linear history that `git bisect` and the changelog rely on.
- **Rebase merges.** Rejected: each unverified `Plan step` checkpoint, which only its own step's check verified, would
  land on `main`.
- **Requiring every status, including Codecov and `copilot-setup-steps`.** Rejected: see decision 3.
- **Opening a pull request ready with auto-merge on.** Rejected: the author's review is the approval, and an agent must
  not give it.
- **A label-triggered workflow that submits review states.** Rejected: unresolved threads and the draft state already
  carry the author's decision without extra automation.
- **Squash through the merge queue (chosen).** A linear `main` with one commit per pull request, each the merged result
  that the full gate passed, under a conventional commit subject.

## Consequences

- The push protocol pushes a topic branch and opens or updates its pull request; it never pushes `main`.
- A workflow added later whose job gates merges is added to the ruleset's required checks, and it must report on every
  pull request, as Frontend CI does.
- A merge to `main` waits for the required checks, and `git log` on `main` reads as one conventional commit per pull
  request; the step commits remain on the merged pull request.
- A trailer that a tool adds to a branch commit can reach `main` through the squash commit.

## References

- [ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) — the merge queue runs
  the full gate.
- [ADR-1170](1170-agent-work-is-planned-on-claude-opus-and-executed-step-by-step-on-claude-sonnet.md) — the
  plan-and-execute workflow whose push protocol opens the pull request.
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md) — the
  greenfield pre-release posture.
- [project-template ADR-0005](project-template/0005-the-default-branch-accepts-changes-only-through-pull-requests.md),
  [project-template ADR-0006](project-template/0006-a-person-merges-every-pull-request.md),
  [project-template ADR-0013](project-template/0013-pull-requests-merge-with-a-merge-commit-or-by-rebasing.md) and
  [project-template ADR-0018](project-template/0018-agent-attribution-is-avoided-not-enforced.md).
- [project-template ADR-0001](project-template/0001-conventions-and-lessons-live-in-the-repository.md) — why the
  workflow is recorded in the repository.
- [`GOVERNANCE.md`](../../GOVERNANCE.md) — change control and the required checks.
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — the day-to-day contributor workflow.
- [NCodeGroup/project-template#200](https://github.com/NCodeGroup/project-template/issues/200).
