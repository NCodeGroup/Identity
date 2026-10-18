# 125. Trunk-based development on a single `main` trunk

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode Group

## Context

The repository is governed by a two-branch, GitFlow-shaped model: a released line plus a long-lived `dev` integration
branch for in-progress work, with the released line protected behind pull-request review requiring at least one
maintainer approval and no self-approval. Three standing facts make that model inert rather than protective:

1. **Nothing is released.** The family is pre-1.0 with no stable tag and no external consumers
   ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)). A long-lived integration branch exists to stage work
   away from a trunk that external consumers are already building against. With no such consumers, the integration
   branch is a second name for the trunk plus a merge step, and the two branches drift for no benefit.
2. **The approval rule is unsatisfiable.** A single-maintainer project cannot produce "at least one approval, no
   self-approval." The rule does not raise the quality bar; it is simply never met, which makes the written governance
   describe a process the repository does not follow. Review happens, but as automated review on the change itself
   rather than as a second human's approval.
3. **Tooling keys off the default branch.** Repository host defaults — build status, dependency and code scanning, the
   landing page — attach to the default branch, and Nerdbank.GitVersioning's `publicReleaseRefSpec`
   ([`version.json`](../../version.json)) matches the trunk, release lines, and version tags but never matched the
   integration branch. Working off the integration branch therefore stamps every build with a commit-height suffix and
   points the host's reporting at a branch that receives no commits.

The question is forced now because provisioning and CI/CD encode the branch model directly: workflow triggers,
image tags, and deployment gates all have to name a branch.

## Decision

**Develop on one trunk, `main`.** There is no long-lived integration branch. Work lands on `main` directly, or through
a **short-lived** branch opened when a change benefits from review on a pull request; such a branch targets `main` and
is deleted on merge. The prior default branch (`master`) is an ancestor of the trunk and is fully contained in it, so it
is deleted rather than maintained or renamed.

**Leave the trunk unprotected while pre-release.** `main` carries no branch protection: no required status check and
no required approval. CI reports a verdict on every commit, but nothing blocks a push. Two reasons. First, the gate
that would be required cannot be satisfied anyway — a single maintainer cannot produce an approval — and a required
status check on a repository where every push is made by the person who ran the same gate locally only adds latency to
a verdict already known. Second, and decisively, protection exists to stop a broken trunk reaching **consumers**;
nothing is published, so a broken commit costs one person one fix-forward commit.

Protection is adopted at the **first stable release**, not before: a required `Definition of Done` check, and an
approval requirement once a second maintainer exists. That is the same trigger that ends the pre-release latitude in
[ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md), so the two move together.

**Gate deployment with an environment, not a branch.** A branch is a poor deployment gate: it conveys no approval
and no audit record. Promotion to a hosted environment is gated by a deployment environment with its own required
reviewer and secrets, so the approval decision is recorded against the deployment rather than implied by a merge.

**Reintroduce an integration branch on either trigger:** a second regular contributor, or the first stable release.
Both restore the condition the two-branch model is designed for — concurrent work that must be staged away from a trunk
others depend on.

## Options considered

- **Keep the integration branch.** Rejected: it buys isolation that nothing currently needs, splits the history across
  two branches that must be kept in sync by hand, and leaves the host's reporting pointed at an idle default branch.
- **Keep the integration branch and make it the default branch.** Rejected: it fixes the reporting mismatch but leaves
  two branches whose roles are indistinguishable while unreleased, and still fails `publicReleaseRefSpec`, so builds keep
  a commit-height suffix for no reason.
- **Keep requiring pull-request approval on the trunk.** Rejected as written: a single maintainer cannot satisfy it.
  Nor is a required status check adopted in its place while pre-release — see the decision above.
- **Trunk-based development on `main` (chosen).** Matches the actual team size and release state, aligns the default
  branch with the version-stamping and host reporting, and removes a merge step that protects nothing.

## Consequences

- Builds on the trunk match `publicReleaseRefSpec` and carry a clean version with no commit-height suffix, so container
  image tags and package versions derive from the trunk without a hash suffix.
- Continuous integration triggers on `main` and on pull requests targeting it; a deployment workflow promotes from
  `main` through an environment gate.
- Governance and the contributor guide describe a process the repository can actually follow: the trunk is unprotected
  while pre-release, and the enforced gate is the one a contributor runs locally before pushing.
- Release lines are unaffected: a shipped `major.minor` is still serviced from a long-lived `release/vX.Y` branch cut
  with `nbgv prepare-release`, and a release is still an annotated tag. This ADR removes only the *integration* branch,
  not the release-line model.
- The latitude recorded here is scoped to the pre-release period in the same way as
  [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md): the first stable release is an explicit trigger to
  re-evaluate.

## References

- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the greenfield pre-release posture whose "no released
  consumers" premise this decision shares. That ADR's decision is unchanged; its incidental references to the former
  integration branch are corrected to name the trunk.
- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — why this stance is recorded in the repository
  rather than left as tribal knowledge.
- [`GOVERNANCE.md`](../../GOVERNANCE.md) — ownership, change control, and the release/branch/tag model this updates.
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — the day-to-day contributor workflow.
- [`version.json`](../../version.json) — `publicReleaseRefSpec`, which matches the trunk, release lines, and tags.
