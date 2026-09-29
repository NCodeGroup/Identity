# Architecture Decision Records

This directory records the **architecturally significant decisions** made for the
**NCode.Identity** library family, using lightweight
[Architecture Decision Records (ADRs)](https://adr.github.io/).

An ADR captures a single decision: the context that forced it, the decision itself, and the
consequences. ADRs are immutable once **Accepted** — if a decision changes, add a new ADR that
**supersedes** the old one (and update the old one's status) rather than editing history.

## Conventions

- Files are named `NNNN-title-in-kebab-case.md`, where `NNNN` is a zero-padded, monotonically
  increasing number.
- Start a new ADR by copying [`0000-template.md`](./0000-template.md).
- **Status** is one of: `Proposed`, `Accepted`, `Deprecated`, or `Superseded by ADR-NNNN`.
- Keep ADRs short, concrete, and link to real code so future readers can verify the decision
  against the current source.

## Index

| ADR                                                                    | Title                                                                     | Status   |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------- | -------- |
| [0001](./0001-mediator-vs-dependency-injection-logic-classes.md)       | Mediator (commands & handlers) vs. dependency-injected logic classes      | Accepted |
| [0002](./0002-ephemeral-development-keys.md)                           | Ephemeral development keys are an explicit opt-in, never a silent default | Accepted |
| [0003](./0003-central-package-management-and-lockstep-versioning.md)   | Central Package Management and lockstep versioning                        | Accepted |
| [0004](./0004-no-cross-assembly-internalsvisibleto.md)                 | No cross-assembly InternalsVisibleTo in production                        | Accepted |
| [0005](./0005-public-api-surface-is-tracked-and-evolves-compatibly.md) | The public API surface is tracked and evolves backward-compatibly         | Accepted |
| [0006](./0006-single-canonical-impl-is-defaultfoo.md)                  | The single canonical implementation of an interface is named DefaultFoo   | Accepted |
| [0007](./0007-provenance-sourcelink-and-symbols.md)                    | Provenance — SourceLink to GitHub and a portable symbol package           | Accepted |
| [0008](./0008-conventions-and-lessons-live-in-the-repository.md)       | Conventions and lessons live in the repository, not in volatile memory    | Accepted |
| [0009](./0009-endpoint-families-own-their-route-group.md)              | Endpoint families own their route group and cross-cutting endpoint filters | Accepted |
