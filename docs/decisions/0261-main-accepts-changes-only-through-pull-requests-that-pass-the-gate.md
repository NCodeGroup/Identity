# 261. main accepts changes only through pull requests that pass the gate

- **Status:** Accepted (strict up-to-date superseded by [ADR-0263](0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md))
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

[ADR-0125](0125-trunk-based-development-on-main.md) leaves `main` unprotected while the family is pre-release, for two
reasons: a single maintainer cannot approve their own pull request, and nothing is published, so a broken commit costs
one fix-forward commit. Three facts outweigh those reasons:

- Every change already travels through a branch and a pull request. Cloud agents propose changes from `copilot/*`
  branches, and Dependabot opens its own pull requests.
- CI runs the gate on every pull request, but a direct push to `main` skips it. The push is the one path by which an
  unchecked commit reaches the trunk.
- The maintainer decides to work through pull requests and to let cloud agents propose changes only that way.

A pull-request rule with **zero** required approvals, together with required status checks, is satisfiable by one
maintainer: it enforces the gate without asking for a review that nobody can give.

## Decision

**`main` accepts changes only through a pull request whose required checks pass on a branch up to date with `main`.**

1. **One repository ruleset protects the trunk.** The ruleset is named `main`, targets the default branch, has the
   enforcement status **active**, and has **no bypass actors**. Its rules:
   - `deletion`: the branch cannot be deleted.
   - `non_fast_forward`: no force push.
   - `required_linear_history`: no merge commit reaches the trunk.
   - `pull_request`: 0 required approvals, stale reviews dismissed on push, every review thread resolved before the
     merge, allowed merge methods `squash` and `rebase`.
   - `required_status_checks`, strict, so the branch is up to date with `main`, with four checks from GitHub Actions:
     `Definition of Done`, `Lint, typecheck, test, build`, `Lint and render the host chart` and `Build host image`.
2. **The repository settings match the ruleset.** Merge commits are disabled; a squash merge takes its title and
   description from the pull request; head branches are deleted on merge; auto-merge and "update branch" are enabled.
3. **A merge keeps history linear.** A pull request merges by squash or by rebase, so `git bisect` walks one line and
   each change appears once.
4. **The required checks are the gate, not every status.** The four checks are the jobs that build, test, lint and
   package the family. Codecov's statuses are not required: Codecov is a third party whose outage would block every
   merge. `copilot-setup-steps` is not required: it runs only when its own workflow file changes, so it reports on few
   pull requests.
5. **A required check reports on every pull request.** A path-filtered workflow that does not run leaves its required
   check pending forever. Frontend CI therefore runs on every pull request into `main`: its first job,
   `Detect frontend changes`, decides from the pull request's changed files whether `Lint, typecheck, test, build`
   runs, and a skipped job reports success. A push to `main` keeps the path filter.
6. **The consolidation merge precedes the ruleset.** PR #9, the branch that consolidated all work done before the
   trunk was protected, merged with a merge commit before the ruleset was enabled. Its commits are verified
   conventional commits that CI checked and that documents cite by hash; a squash would collapse them, and a rebase
   would rewrite every hash.
7. **An approval requirement is added when a second maintainer exists.** That is the trigger ADR-0125 names for
   review, and it raises the `pull_request` rule's required approvals to one.

This decision supersedes ADR-0125's decision to leave the trunk unprotected while pre-release. The rest of ADR-0125
stands: one trunk, short-lived branches, release lines, and deployment gated by an environment.

## Options considered

- **Keep ADR-0125's unprotected trunk.** Rejected: a direct push skips the gate that CI already runs.
- **Classic branch protection.** Rejected: a ruleset is the current GitHub mechanism, is visible to anyone who reads
  the repository, and composes with other rulesets.
- **Require one approval.** Rejected: a single maintainer cannot satisfy it (ADR-0125).
- **Allow merge commits.** Rejected: linear history keeps `git bisect` and the changelog readable.
- **Require every status, including Codecov and `copilot-setup-steps`.** Rejected (decision 4).

## Consequences

- Every change — a person's, a cloud agent's, Dependabot's — reaches `main` only through a green, up-to-date pull
  request.
- An administrator changes the ruleset in the repository settings, and GitHub records the change.
- The push protocol pushes a topic branch and opens or updates its pull request; it never pushes `main`.
- A workflow added later whose job gates merges is added to the ruleset's required checks, and it must report on every
  pull request, as Frontend CI does.
- A merge to `main` waits for the required checks, and a branch behind `main` is updated before it merges.

## References

- [ADR-0125](0125-trunk-based-development-on-main.md) — trunk-based development on `main`, whose unprotected-trunk
  decision this supersedes.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the greenfield pre-release posture.
- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — why the workflow is recorded in the repository.
- [ADR-0170](0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md) — the
  plan-and-execute workflow whose push protocol opens the pull request.
- [`GOVERNANCE.md`](../../GOVERNANCE.md) — change control and the release/branch/tag model.
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — the day-to-day contributor workflow.
