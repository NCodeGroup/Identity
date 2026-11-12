# 1508. A decision record is rewritten when its decision changes and relates only to template records

- **Status:** Accepted
- **Date:** 2026-11-12
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0012](project-template/0012-the-templates-records-are-numbered-below-1000-a-projects-from-1000.md), [project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)
- **Supersedes:** [project-template ADR-0010](project-template/0010-a-change-consumers-can-observe-is-decided-before-it-is-built.md), its consequence that a later reversal is a new record that supersedes the old one

## Context

A record that another record amends or supersedes makes a reader assemble one decision from several files, each partly
out of date. A chain of `Amends`, `Amended by`, `Superseded by` and `Supersedes` lines grows with every change, and a
reader who stops at the first file reads a decision the code does not hold.

The family is pre-release ([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md),
[project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)): nothing it
decided has a released reader that must keep working, and git history keeps every earlier text of a record.

## Decision

**A family record states one decision as it holds in the code. When that decision changes, the record is rewritten in
place, and it relates to no other family record.**

1. **A changed decision rewrites its record.** The title and the file name change with it, so the record reads as if the
   decision had always been so. The record keeps its number.
2. **Two records that come to decide one thing become one record.** It keeps the number of the more-cited one. The other
   number is retired and never reused, and every citation of it in the repository names the record that holds its
   decision.
3. **A family record never relates itself to another family record.** It carries no `Amends`, `Amended by`,
   `Supersedes`, `Superseded by`, `Extends` or `Overrides` line and no such wording, and its Status is `Accepted` or
   `Proposed` with nothing after it. It cites another family record only in its text, as `ADR-NNNN`, where the other
   record holds a rule this one relies on; it never restates that rule.
4. **The only relation lines a family record carries name project template records,** which the family never edits:
   - `Refines` adds the family's specifics to a template record.
   - `Supersedes` decides otherwise than a template record, and `AGENTS.md` lists the override. The line names the
     template decisions it overrides when it overrides only some.
   - `Conflicts with` marks a template record that the code does not yet follow, and names the issue that tracks it.
5. **The index groups the records by area.** `docs/decisions/README.md` holds one table per area, each in number order,
   so a reader finds a decision by its subject.

This record refines the numbering of
[project-template ADR-0012](project-template/0012-the-templates-records-are-numbered-below-1000-a-projects-from-1000.md)
and the pre-release posture of
[project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md). It
supersedes only the consequence of
[project-template ADR-0010](project-template/0010-a-change-consumers-can-observe-is-decided-before-it-is-built.md) that
a later reversal is a new record that supersedes the old one. That record's rule that a change consumers can observe is
decided and recorded before it is built holds in full.

## Options considered

- **Superseding records.** Rejected: this is the chain this record removes. Every change adds a file, and a reader
  assembles the decision from all of them.
- **Amending records in place while keeping the amendment history in the Status line.** Rejected: the history belongs
  to git, and the Status line grows without bound.
- **One record per area.** Rejected: an area holds many independent decisions, and a citation must name one.
- **A record rewritten in place, related only to template records.** Chosen: it leaves one current file per decision,
  and a citation names the decision it relies on.

## Consequences

- A reader opens one record per decision and finds it current.
- A pull request that changes a decision edits the record that holds it, so its review sees the decision's text change
  beside the code.
- `build/adr-index.ps1` fails a family record that relates to another family record, carries another header line or has
  text after its Status, and a tracked citation of a number that names no record, so the rule holds without a reviewer.
- `docs/decisions/0000-template.md` and the conventions in `docs/decisions/README.md` describe this shape.
- Smell: a record whose text describes how the decision differs from an earlier one, or two records that state the same
  rule.

## References

- `docs/decisions/README.md`, `docs/decisions/0000-template.md`, `build/adr-index.ps1`
- [project-template ADR-0001](project-template/0001-conventions-and-lessons-live-in-the-repository.md),
  [project-template ADR-0010](project-template/0010-a-change-consumers-can-observe-is-decided-before-it-is-built.md),
  [project-template ADR-0012](project-template/0012-the-templates-records-are-numbered-below-1000-a-projects-from-1000.md),
  [project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)
