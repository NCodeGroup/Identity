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

| ADR | Title | Status |
|-----|-------|--------|
| [0001](./0001-mediator-vs-dependency-injection-logic-classes.md) | Mediator (commands & handlers) vs. dependency-injected logic classes | Accepted |
