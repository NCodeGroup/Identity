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
- Write each ADR as a **timeless, present-tense record** of the decision and its rationale — never a
  narrative of the development process. Avoid development-timeline language (_"shipped"_, _"reversed
  before release"_, _"the first cut"_, _"we built"_, _"now/originally/today"_): describe what the
  decision **is** and why, so the record reads the same a year later. A superseded ADR keeps its
  original decision text and only gains a status change plus a forward pointer to its successor.

## Index

| ADR                                                                            | Title                                                                                              | Status                                                                             |
| ------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| [0001](./0001-mediator-vs-dependency-injection-logic-classes.md)               | Mediator (commands & handlers) vs. dependency-injected logic classes                               | Accepted                                                                           |
| [0002](./0002-ephemeral-development-keys.md)                                   | Ephemeral development keys are an explicit opt-in, never a silent default                          | Accepted                                                                           |
| [0003](./0003-central-package-management-and-lockstep-versioning.md)           | Central Package Management and lockstep versioning                                                 | Accepted                                                                           |
| [0004](./0004-no-cross-assembly-internalsvisibleto.md)                         | No cross-assembly InternalsVisibleTo in production                                                 | Accepted                                                                           |
| [0005](./0005-public-api-surface-is-tracked-and-evolves-compatibly.md)         | The public API surface is tracked and evolves backward-compatibly                                  | Accepted                                                                           |
| [0006](./0006-single-canonical-impl-is-defaultfoo.md)                          | The single canonical implementation of an interface is named DefaultFoo                            | Accepted                                                                           |
| [0007](./0007-provenance-sourcelink-and-symbols.md)                            | Provenance — SourceLink to GitHub and a portable symbol package                                    | Accepted                                                                           |
| [0008](./0008-conventions-and-lessons-live-in-the-repository.md)               | Conventions and lessons live in the repository, not in volatile memory                             | Accepted                                                                           |
| [0009](./0009-endpoint-families-own-their-route-group.md)                      | Endpoint families own their route group and cross-cutting endpoint filters                         | Accepted                                                                           |
| [0010](./0010-supported-settings-unset-means-unrestricted.md)                  | `*_supported` settings: unset means unrestricted; a replaceable baseline supplies defaults         | Accepted                                                                           |
| [0011](./0011-secret-management-api.md)                                        | Secret management: server-side key generation, no material on the surface                          | Accepted                                                                           |
| [0012](./0012-interceptor-managed-concurrency-tokens.md)                       | Concurrency tokens are interceptor-managed; sub-resource version columns stay manual               | Accepted                                                                           |
| [0013](./0013-management-precondition-mediator-pipeline.md)                    | Management preconditions are a mediator validation pipeline                                        | Superseded by [0015](./0015-management-core-validation-via-validators.md)          |
| [0014](./0014-server-generated-opaque-resource-ids.md)                         | Resource identifiers are server-generated and opaque                                               | Accepted                                                                           |
| [0015](./0015-management-core-validation-via-validators.md)                    | Management core validation via replaceable validators                                              | Accepted                                                                           |
| [0016](./0016-implementation-packages-depend-only-on-abstractions.md)          | Implementation packages depend only on abstractions; composition wires them                        | Accepted                                                                           |
| [0017](./0017-tenant-resolution-shared-abstraction-and-management-boundary.md) | Tenant resolution is a shared abstraction; the management API has an optional tenant boundary      | Superseded by [0018](./0018-tenant-scoped-data-access-at-the-persistence-layer.md) |
| [0018](./0018-tenant-scoped-data-access-at-the-persistence-layer.md)           | Tenant selection is shared; tenant-bound data access is scoped at the persistence layer            | Accepted                                                                           |
| [0019](./0019-collection-endpoints-keyset-pagination.md)                       | Collection endpoints use keyset (cursor) pagination, not a query protocol                          | Accepted                                                                           |
| [0020](./0020-grant-management-read-surface.md)                                | Grant administration is a tenant-scoped, metadata-only read surface with opaque ids                | Accepted                                                                           |
| [0021](./0021-developer-signing-keys-seeded-through-persistence.md)            | Developer signing keys are seeded through the persistence layer, not injected at the read path     | Accepted                                                                           |
| [0022](./0022-grant-filters-and-bulk-revocation.md)                            | Grant administration gains specific subject/client filters and bulk revocation                     | Accepted                                                                           |
| [0023](./0023-scopes-and-resources-management-model.md)                        | Scopes and resources: an Auth0-style Resource Server owns its scopes; a Client Grant authorizes    | Accepted                                                                           |
| [0024](./0024-control-plane-and-per-tenant-planes.md)                          | Deployment model: a control plane and per-tenant planes, so a tenant can be isolated to its own DB | Accepted                                                                           |
| [0025](./0025-pre-release-posture-and-auth0-parity-plus.md)                    | Pre-release posture: greenfield on dev, and an Auth0-parity-plus design philosophy                 | Accepted                                                                           |
| [0026](./0026-scope-enforcement-via-resource-servers-and-client-grants.md)     | Scope enforcement is driven by resource servers and client grants, not a scopes_supported setting  | Accepted                                                                           |
| [0027](./0027-access-token-audience-from-resource-servers.md)                  | An access token's audience is the resource servers that own its scopes                             | Accepted                                                                           |
