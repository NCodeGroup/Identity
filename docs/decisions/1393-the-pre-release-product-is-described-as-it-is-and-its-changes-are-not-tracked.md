# 1393. The pre-release product is described as it is, and its changes are not tracked

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)
- **Conflicts with:** the project template's changelog rule in
  [`project-template.instructions.md`](../../.github/instructions/project-template.instructions.md), which groups
  `## [Unreleased]` under `### Changed`, `### Deprecated`, `### Removed` and `### Fixed` as well as `### Added` and
  `### Security`, and the `**Breaking:**` entry label its planning commands write; tracked in issue to be filed

## Context

The family is greenfield and pre-release: nothing is shipped, and a breaking change needs no migration path until the
first stable release ([ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)). Text that describes the product
as a delta from an earlier state — a `**Breaking:**` changelog entry, "renamed `X` to `Y`", a migration note, an XML
doc that says what a type replaces — addresses a reader who used that earlier state. No such reader exists: every
earlier state is an unreleased commit on `main`.

Delta text costs every real reader. It names members that are absent from the code, it goes stale on the next change
to the same area, and a reader replays the history of entries to learn what the product does. Keep a Changelog's
`### Changed`, `### Deprecated`, `### Removed` and `### Fixed` groups and the `**Breaking:**` label compare a version
with a released predecessor, which the family does not have. The conventional-commit `!` marker on a commit subject or
pull request title, which the squash merge copies onto `main`
([ADR-1304](1304-pull-requests-into-main-merge-by-squash-through-the-merge-queue.md)), tells release tooling and
readers that consumers must change something, and there are no consumers.

## Decision

1. **Every text the repository holds describes the product as it is.** A changelog entry, a document, a code comment,
   an XML doc, the text of a decision record, a test name, a commit message and a pull request's title and description
   each say what a package, type, setting, endpoint or behavior is and does, never how it differs from an earlier
   unreleased state. They carry none of these: a `**Breaking:**` label; "now", "new" or "newly" set against an earlier
   state, "no longer", "previously", "used to", "replaces", "is replaced by", "instead of the old", "renamed", "moved",
   "migrated", "before this change" or "after this change"; the name of a removed or renamed member; an upgrade,
   migration, rollback or compatibility note; a "keeps working until" note. The words that name the product's own
   mechanisms stay: an EF Core migration, a key that rotation replaces, a refresh token that a refresh replaces and a
   new session are behavior the product has, described as such.

2. **A fix to unreleased behavior is not tracked.** No entry, comment or document records what was wrong. The behavior
   the fix establishes is described as a capability or a protection; when the text already describes it, the fix adds
   no text.

3. **Until the first stable release, `## [Unreleased]` in `CHANGELOG.md` is a present-tense catalog of what the family
   does.** It has two groups: `### Added`, the capabilities, and `### Security`, the security posture and protections.
   A change edits the catalog in place so that it matches the code: it adds the entry for a capability, rewrites the
   entry whose capability it reshapes, and deletes the entry whose capability it removes. The file keeps the Keep a
   Changelog structure, so the template's tooling reads it.

4. **Commit subjects and pull request titles never carry the conventional-commit `!` breaking marker**, and no commit
   message or pull request description carries a `BREAKING CHANGE:` footer. The subject is a conventional commit
   subject, such as `feat: sign ID tokens with ES256`, that states what the change makes the product do.

5. **The decision-record mechanism stays.** A record's `Status`, `Amends`, `Supersedes`, `Refines` and
   `Conflicts with` lines, and a superseded record's forward pointer, relate decisions to each other; they are not
   change tracking. A record's Context states its forces in the present tense: what a design does and why that is
   wrong, not what the code did.

6. **The rule spans the pre-release period [ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md) defines.**
   From the first stable tag, a version that consumers hold exists, and Keep a Changelog's full grouping records the
   changes to it.

## Options considered

- **The template's Keep a Changelog grouping with `**Breaking:**` labels.** Rejected: it writes for upgraders who do
  not exist, its entries name members absent from the code, and the section grows into a history that a reader replays
  to learn what the product does.
- **A catalog changelog, with breaking changes marked only on commits by `!`.** Rejected: the squash merge copies the
  marker onto `main`, where tooling reads it as a major-version signal against a version nobody consumes, and the
  marker frames the product as a delta all the same.
- **No changelog until the first stable release.** Rejected: the catalog tells reviewers and the first release's
  readers what the family does, and the template's tooling expects the file.
- **Describe the product as it is everywhere (chosen).** Every text reads correctly against the code it sits beside,
  without a history.

## Consequences

- The template's planning commands write `### Changed` and `### Fixed` headings and `**Breaking:**` labels. A plan in
  this repository names this record in its Context for the executor, its changelog step edits the catalog under
  `### Added` and `### Security`, and its review rejects the other headings, the label and the `!` marker. The conflict
  with the template is tracked in issue to be filed.
- A reviewer rejects delta phrasing in any text a change writes, as decision 1 lists it.
- Accepted records keep their text, as the decision-record convention requires; a record that amends or supersedes one
  states its own decision in the present tense.
- `PublicAPI.Unshipped.txt` churn stays the routine signal of pre-release evolution
  ([ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)); it is a build artifact, not prose.

## References

- [ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)
- [ADR-1005](1005-public-api-surface-is-tracked-and-evolves-compatibly.md)
- [ADR-1304](1304-pull-requests-into-main-merge-by-squash-through-the-merge-queue.md)
- [project-template ADR-0001](project-template/0001-conventions-and-lessons-live-in-the-repository.md)
- [`project-template.instructions.md`](../../.github/instructions/project-template.instructions.md)
- [`CHANGELOG.md`](../../CHANGELOG.md)
- [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/)
- [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/)
